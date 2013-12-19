//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Transactions;
using System.Threading;


namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbChannelListener : ChannelListenerBase<IInputSessionChannel>
    {
        SsbUri listenUri;
        string connstring;
        int maxMessageSize;
        BufferManager bufferManager;
        MessageEncoderFactory messageEncoderFactory;
        ServiceInfo serviceInfo;
        SsbBindingElement bindingElement;
        ManualResetEvent cancelEvent = new ManualResetEvent(false);
        
        BindingContext originalBindingContext;

        delegate IInputSessionChannel AcceptChannelDelegate(TimeSpan timeout);
        public SsbChannelListener(SsbBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            this.bindingElement = bindingElement;
            originalBindingContext = context.Clone();
            //Address stuff
            if (context.ListenUriMode == ListenUriMode.Unique)
            {
                throw new ProtocolException("ListenUriMode.Unique is not supported. You must provide an explicit address");
            }
            Uri baseAddress = context.ListenUriBaseAddress;
            if (baseAddress == null)
            {
                throw new ProtocolException("Address is null. You must provide an exlpicit address");
            }
            
   
            listenUri = new SsbUri(BuildUri(baseAddress, context.ListenUriRelativeAddress));

            //copy properties from binding element
            // TODO how do we enforce MaxReceivedMessgeSize
            this.maxMessageSize = (int)bindingElement.MaxReceivedMessageSize;

            this.connstring = bindingElement.SqlConnectionString;
            this.bufferManager = BufferManager.CreateBufferManager(bindingElement.MaxBufferPoolSize, this.maxMessageSize);
            MessageEncodingBindingElement messageEncoderBindingElement = context.BindingParameters.Remove<MessageEncodingBindingElement>();
            if (messageEncoderBindingElement != null)
            {
                this.messageEncoderFactory = messageEncoderBindingElement.CreateMessageEncoderFactory();
            }
            else
            {
                this.messageEncoderFactory = SsbConstants.DefaultMessageEncoderFactory;
            }
        }

        internal Binding CreateResponseBinding()
        {
            BindingElementCollection be = originalBindingContext.Binding.CreateBindingElements();
            if (be.Find<SsbBindingElement>() == null)
            {
                throw new InvalidOperationException("SsbBindingElement not found");
            } 
            return new CustomBinding(be);
        }
        protected override IInputSessionChannel OnAcceptChannel(TimeSpan timeout)
        {
            if (this.State == CommunicationState.Closed || this.State == CommunicationState.Closing)
            {
                return null;
            }
            if (!bindingElement.IsConversationGroupSpecified)
            {
                SsbConversationGroupReceiver cg = this.AcceptConversationGroup(timeout);
                if (cg == null)
                {
                    return null;
                }
                else
                {
                    return new SsbInputSessionChannel(this, cg, this.bufferManager, this.messageEncoderFactory.Encoder, this.listenUri.Uri);
                }
            }
            else //open a specified conversation group and wait for a message
            {
                SsbConversationGroupReceiver cg = this.AcceptConversationGroup(bindingElement.ConversationGroupId,timeout);
                if (cg == null)
                {
                    return null;
                }
                else
                {
                    return new SsbInputSessionChannel(this, cg, this.bufferManager, this.messageEncoderFactory.Encoder, this.listenUri.Uri);
                }

            }
        }

        protected override bool OnWaitForChannel(TimeSpan timeout)
        {

            return true;

        }

        public override Uri Uri
        {
            get { return this.listenUri.Uri; }
        }

        protected override void OnAbort()
        {
            cancelEvent.Set();
        }

        protected override void OnClose(TimeSpan timeout)
        {
            cancelEvent.Set();
        }
        protected override void OnOpen(TimeSpan timeout)
        {
            this.serviceInfo = SsbHelper.GetServiceInfo(this.listenUri.Service , timeout, this.connstring);
        }
        internal SsbConversationGroupReceiver AcceptConversationGroup(Guid conversationGroupId, TimeSpan timeout)
        {

            TimeoutHelper helper = new TimeoutHelper(timeout);

            TransactionOptions to = new TransactionOptions();
            to.IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted;
            to.Timeout = TimeSpan.MaxValue;


            CommittableTransaction tx = new CommittableTransaction(to);
            Transaction currentTrans = Transaction.Current;
            Transaction.Current = tx;
            SqlConnection cn = SsbHelper.GetConnection(this.connstring);
            cn.Open();
            Transaction.Current = currentTrans;

            //first lock the the Conversation Group.  This will prevent other SsbConversationGroupReceivers 
            //from poaching the messages.

            string sql = @"
                    declare @cg uniqueidentifier
                    declare @rc int
                    set @cg = @conversation_group_id

                    exec @rc = sp_getapplock @Resource=@cg, @LockMode='Exclusive'
                    if @rc not in (0,1)
                    begin
                     raiserror('Failed to lock conversation group. sp_getapplock failure code %d.',16,1,@rc);
                    end
                    ";

            SqlCommand cmd = new SqlCommand(sql, cn);
            SqlParameter pConversationGroupId = cmd.Parameters.Add("@conversation_group_id", SqlDbType.UniqueIdentifier);
            pConversationGroupId.Value = conversationGroupId;

            cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();

            //Run the command, but abort if the another thread runs Close or Abort
            IAsyncResult result = cmd.BeginExecuteNonQuery();
            int rc = WaitHandle.WaitAny(new WaitHandle[] { result.AsyncWaitHandle, cancelEvent });
            if (rc == 1) //cancel event
            {
                cmd.Cancel();
                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "Canceling Service Broker wait on SsbChannelListener shutdown", "AcceptConversationGroup");
                cn.Close();
                return null;
            }
            if (rc != 0)
            {
                throw new InvalidOperationException("Unexpected state");
            }
            cmd.EndExecuteNonQuery(result);

            SsbConversationGroupReceiver cg = new SsbConversationGroupReceiver(cn, tx, conversationGroupId, this.serviceInfo,this);
            cg.Open();
            //now wait for a message on the conversation.  Do it here because WCF expects the wait to be here instead of in a subsequent receive.
            //And also there is a different timeout to accepting a channel and waiting for additional messages.
            //But we need to pass in the cancelEvent to abort the listen, if this ChannelListener is closed or aborted
            if (!cg.WaitForFirstMessage(helper.RemainingTime(),cancelEvent))
            {
                if (this.State != CommunicationState.Opened)//this ChannelListener was closed or aborted, so this is expected.
                {                   
                    //for some reason the transaction tx is in an uncommitable state here. Not sure why.
                    //however no other work has occured on this connection, so we can just shut it down.
                    cn.Close();
                    return null; 
                }
                throw new TimeoutException(String.Format("Timed out while waiting for Conversation Group"));
            }
            
            return cg;
        }

        internal SsbConversationGroupReceiver AcceptConversationGroup(TimeSpan timeout)
        {
            TimeoutHelper helper = new TimeoutHelper(timeout);
            try
            {
                //create a transaction to own the conversation.  Open the SqlConnection while
                //the transaction is current, so all the work on the connection will be enlisted 
                //in the transaction.  SqlTransaction doesn't work this way.  It requires every command
                //to be manually enlisted, yuck.
                //As an alternative you could simply issue a new SqlCommand("begin transaction",cn).ExecuteNonQuery()
                //to start a transaction.
                // TODO (dbrowne) Consider whether to allow service-level code to commit this transaction, or, alternatively,
                //commit the transaction after the last conversation message in this session is processed.
                //If service-level code is allowed to commit the transaction then SsbConversationGroupReciver _must_
                //retrieve only a single message in each batch.  Else it could fetch 2 messages and risk the service-level
                //commiting after the first message.

                
                CommittableTransaction tx = new CommittableTransaction(TimeSpan.MaxValue);
                Transaction currentTrans = Transaction.Current;
                Transaction.Current = tx;
                SqlConnection cn = SsbHelper.GetConnection(this.connstring);
                cn.Open();
                Transaction.Current = currentTrans;
                
                tx.TransactionCompleted += new TransactionCompletedEventHandler(tx_TransactionCompleted);

                //wait for a new message, but if that message is in a conversation group that another SsbChannelListener is waiting on
                //then roll back the conversation group lock and get back in line.  The waiting SsbChannelListener should pick it up.
                string sql = String.Format(@"
                    retry:
                    save transaction swiper_no_swiping
                    declare @cg uniqueidentifier
                    declare @rc int

                    waitfor (get conversation group @cg from [{0}]), TIMEOUT @timeout
                    if @cg is null
                    begin
                      return --timeout
                    end 

                    exec @rc = sp_getapplock @Resource=@cg, @LockMode='Exclusive', @LockTimeout = 0
                    if @rc = -1
                    begin
                     print 'skipping message for locked conversation_group'
                     rollback transaction swiper_no_swiping
                     goto retry
                    end
                    
                    set @conversation_group_id = @cg
                    ", this.serviceInfo.QueueName);

                SqlCommand cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = 0;

                cn.InfoMessage += new SqlInfoMessageEventHandler(cn_InfoMessage);
                SqlParameter pTimeout = cmd.Parameters.Add("@timeout", SqlDbType.Int);
                pTimeout.Value = TimeoutHelper.ToMilliseconds(timeout);

                SqlParameter pConversationGroupId = cmd.Parameters.Add("@conversation_group_id", SqlDbType.UniqueIdentifier);
                pConversationGroupId.Direction = ParameterDirection.Output;


                //Run the command, but abort if the another thread runs Close or Abort
                IAsyncResult result = cmd.BeginExecuteNonQuery();
                int rc = WaitHandle.WaitAny(new WaitHandle[] { result.AsyncWaitHandle, cancelEvent });
                if (rc == 1) //cancel event
                {
                    cmd.Cancel();
                    TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose,"Canceling Service Broker wait on SsbChannelListener shutdown","AcceptConversationGroup");
                    cn.Close();
                    return null;
                }
                if (rc != 0)
                {
                    throw new InvalidOperationException("Unexpected state");
                }
                cmd.EndExecuteNonQuery(result);

                if (pConversationGroupId.Value == null || pConversationGroupId.Value.GetType() != typeof(Guid))
                {
                    throw new TimeoutException(String.Format("Timed out while waiting for Conversation Group"));
                }
                
                Guid conversationGroupId = (Guid)pConversationGroupId.Value;

                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, string.Format("Accepted conversation group {0}", conversationGroupId), "AcceptConversationGroup");
                SsbConversationGroupReceiver cg = new SsbConversationGroupReceiver(cn, tx, conversationGroupId, this.serviceInfo,this);
                return cg;
            }
            catch (SqlException ex)
            {

                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while waiting for Conversation Group. Timeout value was {0} seconds",timeout.TotalSeconds), ex);
                }
                else
                {
                    throw new ProtocolException("An error occurred while waiting for a Conversation Group", ex);
                }
            }
        }

        void tx_TransactionCompleted(object sender, TransactionEventArgs e)
        {

            TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, string.Format("SsbChannelListener Transaction completed - {0}",e.Transaction.TransactionInformation.Status), "tx_TransactionCompleted");
        }

        void cn_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "SqlConnection.InfoMessage " + e.Message, "cn_InfoMessage");
        }

        static Uri BuildUri(Uri baseAddress, string relativeAddress)
        {

            if (baseAddress == null)
                throw new ArgumentNullException("baseAddress");

            if (relativeAddress == null)
                throw new ArgumentNullException("relativeAddress");

            if (!baseAddress.IsAbsoluteUri)
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    "Base address must be an absolute URI."), "baseAddress");

            if (baseAddress.Scheme != SsbConstants.Scheme)
            {
                // URI schemes are case-insensitive, so try a case insensitive compare now
                if (string.Compare(baseAddress.Scheme, SsbConstants.Scheme, true, System.Globalization.CultureInfo.InvariantCulture) != 0)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        "Invalid URI scheme: {0}.  Must be {1}.", baseAddress.Scheme, SsbConstants.Scheme), "baseAddress");
                }
            }

            Uri fullUri = baseAddress;

            // Ensure that baseAddress Path does end with a slash if we have a relative address
            if (relativeAddress != string.Empty)
            {
                if (!baseAddress.AbsolutePath.EndsWith("/"))
                {
                    UriBuilder uriBuilder = new UriBuilder(baseAddress);
                    uriBuilder.Path = uriBuilder.Path + "/";
                    baseAddress = uriBuilder.Uri;
                }

                fullUri =  new Uri(baseAddress, relativeAddress);
            }

            return fullUri;


            //lock (base.ThisLock)
            //{
            //    ThrowIfDisposedOrImmutable();
            //    this.listenUri = fullUri;


            //}
        }

        #region AsyncWrappers

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            AcceptChannelDelegate d = new AcceptChannelDelegate(this.AcceptChannel);
            IAsyncResult result = d.BeginInvoke(timeout, callback, state);
            return result;
        }

        protected override IInputSessionChannel OnEndAcceptChannel(IAsyncResult result)
        {
            System.Runtime.Remoting.Messaging.AsyncResult aresult = result as System.Runtime.Remoting.Messaging.AsyncResult;
            if (aresult == null)
            {
                throw new ArgumentException("Invalid IAsyncResult type");
            }
            AcceptChannelDelegate d = aresult.AsyncDelegate as AcceptChannelDelegate;
            return d.EndInvoke(result);

        }

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        protected override bool OnEndWaitForChannel(IAsyncResult result)
        {
            throw new Exception("The method or operation is not implemented.");
        }

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
