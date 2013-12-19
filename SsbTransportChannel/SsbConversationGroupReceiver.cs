//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.ServiceModel.Channels;
using System.Data;
using System.ServiceModel;
using System.Threading;
using System.Collections;
using System.Transactions;

namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbConversationGroupReceiver : CommunicationObject
    {
        SqlConnection con;
        CommittableTransaction  tx;
        SqlDataReader rdr;
        Guid cgId;
        ServiceInfo serviceInfo;
        object rdrlock;
        ManualResetEvent cancelEvent = new ManualResetEvent(false);
        SsbChannelListener channelListener;

        class ReceivedMessage
        {
            public int MessageLength;
            public SsbConversationContext Conversation;
            public byte[] MessageBody;
        }

        ReceivedMessage queuedResult;

        internal SsbConversationGroupReceiver(SqlConnection cn, CommittableTransaction tx, Guid cgId, ServiceInfo serviceInfo, SsbChannelListener channelListener)  
        {
            this.con = cn;
            this.tx = tx;
            this.cgId = cgId;
            this.serviceInfo = serviceInfo;
            this.rdrlock = new object();
            this.channelListener = channelListener;
            
        }

        /// <summary>
        /// This method is only called if the SsbInputSessionChannel is configured to listen on a specific 
        /// Conversation Group.  In this case we will have to wait on a RECIEVE instead of on GET CONVERSATION GROUP
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        internal bool WaitForFirstMessage(TimeSpan timeout, WaitHandle waitCancelEvent)
        {
            ThrowIfDisposedOrNotOpen();

            if (queuedResult != null)
            {
                throw new InvalidOperationException("There is already a queued result");
            }
            int messageLength;
            SsbConversationContext conversation;

            //respect the timeout here since we expect to have to wait on the first message.
            byte[] messageBody = Receive(timeout, null, out messageLength, out conversation, waitCancelEvent);
            if (messageBody == null)
            {
                return false;
            }
            queuedResult = new ReceivedMessage();
            queuedResult.Conversation = conversation;
            queuedResult.MessageBody = messageBody;
            queuedResult.MessageLength = messageLength;
            return true;

        }
        internal byte[] Receive(TimeSpan timeout, BufferManager bm, out int messageLength, out SsbConversationContext conversation)
        {
            return Receive(timeout, bm, out messageLength, out conversation, cancelEvent);
        }
        internal byte[] Receive(TimeSpan timeout, BufferManager  bm, out int messageLength, out SsbConversationContext conversation,WaitHandle cancelEvent)
        {
            ThrowIfDisposedOrNotOpen();

            byte[] message = null;
            conversation = null;
            messageLength = 0;

            if (queuedResult != null)
            {
                ReceivedMessage r = queuedResult;
                queuedResult = null;
                conversation = r.Conversation;
                messageLength = r.MessageLength;
                byte[] buf = bm.TakeBuffer(messageLength);
                Buffer.BlockCopy(r.MessageBody, 0, buf, 0, messageLength);
                return buf;
            }
            try
            {
                lock (rdrlock)
                {
                    

                    //If this is the first time, open the reader, otherwise read the next row
                    if ( rdr == null || !rdr.Read() )
                    {
                        //the last bactch has been processed.  Close the reader.
                        if (rdr != null)
                        {
                            rdr.Close();
                        }

                        rdr = GetMessageBatch(timeout,cancelEvent);

                        //this is a timeout condition
                        //caused by aborting or closing the reciever
                        if ( rdr == null )
                        {
                            return null;
                        }

                        //this is a timeout condition caused by the WAITFOR expiring
                        if( !rdr.Read() )
                        {
                            rdr.Close();

                            //return the Receiver to it's initial state.
                            rdr = null;
                            return null;
                        }
                    }


                    int i = 0;
                    int conversation_handle = i++;
                    int service_name = i++;
                    int message_type_name = i++;
                    int message_body = i++;
                    int message_sequence_number = i++;
                    Guid conversationHandle = rdr.GetGuid(conversation_handle);
                    string ServiceName = rdr.GetString(service_name);
                    string messageTypeName = rdr.GetString(message_type_name);
                    if (messageTypeName != SsbConstants.SsbEndDialogMessage  && messageTypeName != SsbConstants.SsbDialogTimerMessage)
                    {
                                             

                        //this eliminates a one copy because the message_body 
                        //isn't copied into the row buffer.  Instead it's chunked directly
                        //into the message byte[].
                        // CONSIDER (dbrowne) wraping the reader in a custom stream implementation
                        //and pass that back instead to eliminate another copy.
                        messageLength = (int)rdr.GetBytes(message_body, 0, null, 0, 0);
                        if (bm == null)
                        {
                            message = new byte[messageLength];
                        }
                        else
                        {
                            message = bm.TakeBuffer(messageLength);
                        }
                        int br = (int)rdr.GetBytes(message_body, 0, message, 0, messageLength);
                        if (br != messageLength) //should never happen
                        {
                            throw new Exception("Failed to read all the message bytes");
                        }
                    }

                    long sequence = rdr.GetInt64(message_sequence_number);
                    conversation = new SsbConversationContext(conversationHandle, this.cgId, sequence, messageTypeName);

                    if (messageTypeName == SsbConstants.SsbErrorMessage)
                    {
                        System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                        doc.Load(new System.IO.MemoryStream(message, 0, messageLength));

                        System.Xml.XmlNamespaceManager mgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
                        mgr.AddNamespace("er", SsbConstants.SsbErrorMessage);
                        int code = int.Parse(doc.SelectSingleNode("/er:Error/er:Code", mgr).InnerText);
                        string ermsg = doc.SelectSingleNode("/er:Error/er:Description", mgr).InnerText;

                        throw new ProtocolException(string.Format("Service Broker Error Message {0}: {1}", code, ermsg));
                    }

                    SsbInstrumentation.MessageRecieved(messageLength);
                    return message;

                    //if (messageTypeName == SsbConstants.SsbDialogTimerMessage)
                    //{
                    //    //well now we're running again after a lag, now what?
                        
                    //}
                    
                }
                
            }
            catch (SqlException ex)
            {
                //the timeout will not result in a SqlExecption since the timeout value is passed to WAITFOR RECIEVE
                //if (!helper.IsTimeRemaining)
                //{
                //    throw new TimeoutException(String.Format("Timed out while receiving. Timeout value was {0} seconds", timeout.TotalSeconds), ex);
                //}
                
              throw new CommunicationException(String.Format("An exception occurred while receiving from conversation group {0}.", this.cgId), ex);
            }
        }
        

      /// <summary>
      /// This is the method that actually receives Service Broker messages.
      /// </summary>
      /// <param name="timeout">Maximum time to wait for a message.  This is passed to the RECIEVE command, not used as a SqlCommandTimeout</param>
      /// <param name="cancelEvent">An event to cancel the wait.  Async ADO.NET is used to enable the thread to wait for either completion or cancel</param>
      /// <returns></returns>
        SqlDataReader GetMessageBatch(TimeSpan timeout, WaitHandle cancelEvent)
        {
            string SQL = string.Format(@"
            waitfor( 
                RECEIVE conversation_handle,service_name,message_type_name,message_body,message_sequence_number 
                FROM [{0}] WHERE conversation_group_id = @cgid
                    ), timeout @timeout", this.serviceInfo.QueueName);
            SqlCommand cmd = new SqlCommand(SQL, this.con);

            SqlParameter pConversation = cmd.Parameters.Add("@cgid", SqlDbType.UniqueIdentifier);
            pConversation.Value = this.cgId;

            SqlParameter pTimeout = cmd.Parameters.Add("@timeout", SqlDbType.Int);

            pTimeout.Value = TimeoutHelper.ToMilliseconds(timeout);

            cmd.CommandTimeout = 0; //honor the RECIEVE timeout, whatever it is.

            
            //Run the command, but abort if the another thread wants to run Close or Abort
            IAsyncResult result = cmd.BeginExecuteReader(CommandBehavior.SequentialAccess);
            int rc = WaitHandle.WaitAny(new WaitHandle[] { result.AsyncWaitHandle, cancelEvent });
            if (rc == 1) //cancel event
            {
                cmd.Cancel();
                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose,"Canceling Service Broker wait on ConversationGroupReciever shutdown","GetMessageBatch");
                return null;
            }
            if (rc != 0)
            {
                throw new InvalidOperationException("Unexpected state");
            }
            return cmd.EndExecuteReader(result);
        }
        public Guid ConversationGroupId
        {
            get { return this.cgId; }
        }

        /// <summary>
        /// Convenience method for configuraing a callback channel.  Since this is not a duplex channel it doesn't
        /// support the declarative callbacks in the normal WCF method.
        /// </summary>
        /// <typeparam name="TContract"></typeparam>
        /// <returns></returns>
        public TContract GetCallback<TContract>()
        {
            ThrowIfDisposedOrNotOpen();
            try
            {
                Binding binding = channelListener.CreateResponseBinding();
                TContract channel = ChannelFactory<TContract>.CreateChannel(binding,new EndpointAddress(SsbUri.Default));
                SsbConversationSender sender = ((IClientChannel)channel).GetProperty<SsbConversationSender>();
                sender.SetConnection(con);
                ((IClientChannel)channel).Open();
                SsbConversationContext conversation = OperationContext.Current.IncomingMessageProperties[SsbConstants.SsbConversationMessageProperty] as SsbConversationContext;
                sender.OpenConversation(conversation.ConversationHandle);
                return channel;
            }
            catch (Exception ex)
            {
                throw new CommunicationException("An error occurred while obtaining callback channel: " + ex.Message, ex);
            }
        }
        public void EndConversation()
        {
            SsbConversationContext conversation = OperationContext.Current.IncomingMessageProperties[SsbConstants.SsbConversationMessageProperty] as SsbConversationContext;
            this.EndConversation(conversation.ConversationHandle, this.DefaultCloseTimeout);
        }

        public void SetConversationTimer(Guid conversationHandle, TimeSpan timerTimeout)
        {
            SsbHelper.SetConversationTimer(conversationHandle, timerTimeout, this.con);
        }

        internal void EndConversation(Guid conversationHandle, TimeSpan timeout)
        {
            ThrowIfDisposedOrNotOpen();
            TimeoutHelper helper = new TimeoutHelper(timeout);
            try
            {
                string SQL = "END CONVERSATION @ConversationHandle";

                using (SqlCommand cmd = new SqlCommand(SQL, con))
                {
                    cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();
                    SqlParameter pConversation = cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier);
                    pConversation.Value = conversationHandle;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while ending conversation {0}. Timeout value was {1} seconds", conversationHandle,timeout.TotalSeconds), ex);
                }
                else
                {
                    throw new CommunicationException(String.Format("An exception occurred while ending conversation {0}.", conversationHandle), ex);
                }
            }
        }

      /// <summary>
      /// Ends the Service Broker conversation and sends a custom error message on the conversation.
      /// Issues this command to SQL SErver
        ///     END CONVERSATION @ConversationHandle WITH ERROR = @errorCode DESCRIPTION = @errorDescription
      /// </summary>
      /// <param name="errorCode">The error code passed to END CONVERSATION</param>
      /// 
      /// <param name="errorDescription">The Error description passed to END CONVERSATION</param>
      /// 
        public void EndConversationWithError(int errorCode, string errorDescription)
        {
            ThrowIfDisposedOrNotOpen();
            SsbConversationContext conversation = OperationContext.Current.IncomingMessageProperties[SsbConstants.SsbConversationMessageProperty] as SsbConversationContext;
            try
            {
                string SQL = "END CONVERSATION @ConversationHandle WITH ERROR = @error DESCRIPTION = @description";

                SqlCommand cmd = new SqlCommand(SQL, con);
                cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier).Value = conversation.ConversationHandle;
                cmd.Parameters.Add("@error", SqlDbType.Int).Value = errorCode;
                cmd.Parameters.Add("@description", SqlDbType.NVarChar, 3000).Value = errorDescription;

                cmd.ExecuteNonQuery();

            }
            catch (SqlException ex)
            {
               throw new CommunicationException(String.Format("An exception occurred while ending conversation {0}.", conversation.ConversationHandle), ex);
            }
        }
   
        protected override TimeSpan DefaultCloseTimeout
        {
            get { return SsbConstants.DefaultCloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return SsbConstants.DefaultOpenTimeout; }
        }

        /// <summary>
        /// The Reciever always needs a transaction to protect its conversation, but a Service may wish to enlist other work
        /// in the transaction.  TakeTransaction will detach the transaction that the conversation uses from the reciever and allow
        /// the server to enlist other work in it and control its fate.  
        /// </summary>
        /// <returns></returns>
        public CommittableTransaction TakeTransaction()
        {
            CommittableTransaction tran = this.tx;
            tx = null;
            return tran;
        }

        /// <summary>
        /// The Reciever always needs a transaction to protect its conversation, but a Service may wish to enlist other work
        /// in the transaction.  GetTransaction will return a non-commitable reference to the Reciever's transaction for the 
        /// service to enlist work in, but the transaction will still be commited on closing the Reciever, or rolled back on
        /// aborting it.
        /// </summary>
        /// <returns></returns>
        public Transaction GetTransaction()
        {
            return this.tx.Clone();
        }

        /// <summary>
        /// This method just allows service code to execute SqlCommands over the connection used by the Reciever.  Any commands executed
        /// over this connection will automatically be enlisted in the Reciever's transaction.
        /// </summary>
        /// <returns></returns>
        public SqlConnection GetConnection()
        {
            return con;
        }

        protected override void OnAbort()
        {
            cancelEvent.Set();
            lock (rdrlock)
            {
                if (this.rdr != null)
                {
                    this.rdr.Dispose();
                }
                if (this.tx != null)
                {
                    this.tx.Rollback();
                }
                if (this.con != null)
                {
                    this.con.Dispose();
                }
            }
        }

        protected override void OnClose(TimeSpan timeout)
        {
            cancelEvent.Set();
            lock (rdrlock)
            {

                if (this.rdr != null)
                {
                    this.rdr.Dispose();
                }
                //The ConversationGroupReciver
                if (this.tx != null && tx.TransactionInformation.Status == TransactionStatus.Active)
                {
                    this.tx.Commit();
                }
                if (this.con != null)
                {
                    this.con.Dispose();
                }
            }
            
        }
        protected override void OnOpen(TimeSpan timeout)
        {

        }
        #region Async Wrappers

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            CloseDelegate d = new CloseDelegate(this.Close);
            return d.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            CloseDelegate d = new CloseDelegate(this.Close);
            d.EndInvoke(result);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            OpenDelegate d = new OpenDelegate(this.Open);
            return d.BeginInvoke(timeout, callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            OpenDelegate d = new OpenDelegate(this.Open);
            d.EndInvoke(result);
        }
        #endregion

    }
}
