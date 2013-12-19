//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Transactions;
namespace Microsoft.Samples.SsbTransportChannel
{
    public sealed class SsbConversationSender : CommunicationObject
    {
        bool ownsConnection = true;
        SqlConnection con;
        string connectionString;
        string source;
        string target;
        ConversationInfo conversation;
        bool endConversationOnClose;
        string contract;
        bool useEncryption;

        /// <summary>
        /// This property indicates whether the service broker conversatoin
        /// will ended when this client is closed.  If a client doesn't explicitly start
        /// a conversation or open an existing conversation this property defaults to true.
        /// 
        /// Set this property to false if the client side of the conversation is going to receive
        /// response messages or end conversation messages.
        /// </summary>
        public bool EndConversationOnClose
        {
            get { return endConversationOnClose; }
            set { endConversationOnClose = value; }
        }
        SsbChannelFactory factory;

        internal SsbConversationSender(string connectionString,Uri via, SsbChannelFactory factory, bool endConversationOnClose, string contract, bool useEncryption)
        {
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }
            if (via == null)
            {
                throw new ArgumentNullException("via");
            }
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            SsbUri ssburi = new SsbUri(via);
            this.source = ssburi.Client;
            this.target = ssburi.Service;
            this.connectionString = connectionString;
            this.endConversationOnClose = endConversationOnClose;
            this.factory = factory;
            this.contract = contract;
            this.useEncryption = useEncryption;
            
        }

        public Guid ConversationGroupID
        {
            get
            {
                ThrowIfDisposedOrNotOpen();
                return conversation.ConversationGroupID;
            }
        }
        public Guid ConversationHandle
        {
            get
            {
                ThrowIfDisposedOrNotOpen();
                if (conversation == null)
                {
                    throw new InvalidOperationException("There is no active conversation.  Either send a message or call BeginConversation, or OpenConversation.");
                }
                return conversation.ConversationHandle;
            }
        }

        public ServiceHost CreateResponseHost<TContract,TService>(Uri listenAddress)
        {
            ThrowIfDisposedOrNotOpen();

            ServiceHost host = new ServiceHost(typeof(TService));
            return CreateResponseHost2<TContract>(host,listenAddress);

        }
        public ServiceHost CreateResponseHost<TContract>(TContract serviceInstance, Uri listenAddress)
        {
            ThrowIfDisposedOrNotOpen();

            ServiceHost host = new ServiceHost(serviceInstance);
            return CreateResponseHost2<TContract>(host, listenAddress);
        }

        private ServiceHost CreateResponseHost2<TContract>(ServiceHost host, Uri listenAddress)
        {
            //if turn this off, since it makes no sense
            //and if the conversation was started automatically, it will be on.
            endConversationOnClose = false;

            //Uri responseAddress = new SsbUri(listenAddress).Reverse();
            BindingElementCollection bindingElements = factory.Binding.CreateBindingElements();
            SsbBindingElement ssbBindingElement = bindingElements.Find<SsbBindingElement>();

            ssbBindingElement.ConversationGroupId = ConversationGroupID;
            Binding responseBinding = new CustomBinding(bindingElements);
            responseBinding.ReceiveTimeout = factory.Binding.ReceiveTimeout;
            responseBinding.OpenTimeout = factory.Binding.OpenTimeout;
            responseBinding.CloseTimeout = factory.Binding.CloseTimeout;
            host.AddServiceEndpoint(typeof(TContract), responseBinding, listenAddress);

            return host;
            
        }
        public SqlConnection GetConnection()
        {
            return this.con;
        }
        internal void Send(byte[] buffer, TimeSpan timeout, string messageType)
        {
            ThrowIfDisposedOrNotOpen();
            TimeoutHelper helper = new TimeoutHelper(timeout);


            //if the client hasn't explicitly begun a conversation, start one here.
            // CONSIDER (dbrowne) automatically ending the conversation in this case.
            if (conversation == null)
            {
                BeginNewConversation();
                this.endConversationOnClose = true;
            }

            try
            {
                string SQL = string.Format(@"SEND ON CONVERSATION @Conversation MESSAGE TYPE [{0}](@MessageBody)",SsbHelper.ValidateIdentifier(messageType));
                SqlCommand cmd = new SqlCommand(SQL, con);

                cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();                
                SqlParameter pConversation = cmd.Parameters.Add("@Conversation", SqlDbType.UniqueIdentifier);
                pConversation.Value = this.conversation.ConversationHandle;

                SqlParameter pMessageBody = cmd.Parameters.Add("@MessageBody", SqlDbType.VarBinary);
                pMessageBody.Value = buffer;

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 8429)
                    {
                        string errorMessage = SsbHelper.CheckConversationForErrorMessage(con,this.conversation.ConversationHandle);
                        if (errorMessage != null)
                        {
                            throw new Exception(errorMessage);
                        }
                        throw;
                    }
                    throw;
                }
                SsbInstrumentation.MessageSent(buffer.Length);

            }
            catch (Exception e)
            {
                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException("Timed out while sending a message. The timeout value passed in was " + timeout.TotalSeconds + " seconds");
                }
                else
                {
                    throw new CommunicationException(String.Format("An exception occurred while sending on conversation {0}.", this.conversation.ConversationHandle), e);
                }
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

        #region AsyncWrappers

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



        protected override void OnAbort()
        {
            if (ownsConnection)
            {
                con.Dispose();
            }

        }
        protected override void OnClose(TimeSpan timeout)
        {
            if (endConversationOnClose && this.conversation != null)
            {
                this.EndConversation();
            }
            if (ownsConnection)
            {
                con.Dispose();
            }
        }

        public void SetConnection(SqlConnection con)
        {

            if (this.State == CommunicationState.Opened)
            {
                throw new InvalidOperationException("Cannot set the connection of an open SsbConversationSender");

            }
            this.connectionString = null;
            this.con = con;
            this.ownsConnection = false; //remember not to close this connection, as it's lifetime is controled elsewhere.
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            TimeoutHelper helper = new TimeoutHelper(timeout);


            if (this.con == null)
            {
                this.con = SsbHelper.GetConnection(this.connectionString);
                con.Open();
                this.ownsConnection = true;
            }

        }

        public void OpenConversation(Guid conversationHandle)
        {
            this.OpenConversation(conversationHandle, this.DefaultOpenTimeout);
        }
        public void OpenConversation(Guid conversationHandle, TimeSpan timeout)
        {
            ThrowIfDisposedOrNotOpen();
            this.conversation = SsbHelper.GetConversationInfo(conversationHandle, con);
        }
        public Guid BeginNewConversation()
        {
            return this.BeginNewConversation(Guid.Empty, this.DefaultOpenTimeout);
        }
        public Guid BeginNewConversation(TimeSpan timeout)
        {
            return this.BeginNewConversation(Guid.Empty, timeout);
        }
        public Guid BeginNewConversation(Guid conversationGroupId)
        {
           return this.BeginNewConversation(conversationGroupId, this.DefaultOpenTimeout);
        }
        public Guid BeginNewConversation(Guid conversationGroupId, TimeSpan timeout)
        {
            ThrowIfDisposedOrNotOpen();
            endConversationOnClose = false; //if a conversation is explicitly started, don't automatically close it.
            TimeoutHelper helper = new TimeoutHelper(timeout);
            try
            {
                


                string SQL = string.Format(
                               @"BEGIN DIALOG CONVERSATION @ConversationHandle 
                               FROM SERVICE @Source TO SERVICE @Target 
                               ON CONTRACT [{0}] WITH ENCRYPTION = {1}",contract,useEncryption?"ON":"OFF");
                if (conversationGroupId != Guid.Empty)
                {
                    SQL += String.Format(", RELATED_CONVERSATION_GROUP = '{0}'", conversationGroupId);
                }
                SqlCommand cmd = new SqlCommand(SQL, con);
                cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();
                SqlParameter pconversationHandle = cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier);
                pconversationHandle.Direction = ParameterDirection.Output;

                SqlParameter pTarget = cmd.Parameters.Add("@Target", SqlDbType.VarChar);
                pTarget.Value = this.target;
                SqlParameter pSource = cmd.Parameters.Add("@Source", SqlDbType.VarChar);
                pSource.Value = this.source;

                cmd.ExecuteNonQuery();

                this.conversation = SsbHelper.GetConversationInfo((Guid)pconversationHandle.Value, con);
                return this.conversation.ConversationHandle;
 
            }
            catch (SqlException ex)
            {

                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while beginning new conversation to service {0}. Timeout value was {1} seconds", this.target, timeout.TotalSeconds), ex);
                }
                else
                {
                    throw new CommunicationException(String.Format("An exception occurred while beginning new conversation to service {0}", this.target), ex);
                }
            }
            finally
            {

            }
        }

        public void SetConversationTimer(Guid conversationHandle, TimeSpan timerTimeout)
        {
            SsbHelper.SetConversationTimer(conversationHandle, timerTimeout, this.con);
        }

        public void EndConversation()
        {
            this.EndConversation(this.DefaultCloseTimeout);
        }
        public void EndConversationWithError(TimeSpan timeout, int errorCode, string errorDescription)
        {

            TimeoutHelper helper = new TimeoutHelper(timeout);
            try
            {
                string SQL = "END CONVERSATION @ConversationHandle WITH ERROR = @error DESCRIPTION = @description";

                SqlCommand cmd = new SqlCommand(SQL, con);
                cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();
                cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier).Value = conversation.ConversationHandle;

                cmd.Parameters.Add("@error", SqlDbType.Int).Value = errorCode;
                cmd.Parameters.Add("@description", SqlDbType.NVarChar, 3000).Value = errorDescription;

                cmd.ExecuteNonQuery();
                this.conversation = null;

            }
            catch (SqlException ex)
            {
                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while ending conversation {0}. Timeout value was {1} seconds", this.conversation.ConversationHandle, timeout.TotalSeconds), ex);
                }
                else
                {
                    throw new CommunicationException(String.Format("An exception occurred while ending conversation {0}.", this.conversation.ConversationHandle), ex);
                }
            }
        }
        public void EndConversation(TimeSpan timeout)
        {
            
            TimeoutHelper helper = new TimeoutHelper(timeout);
            try
            {
                string SQL = "END CONVERSATION @ConversationHandle";

                SqlCommand cmd = new SqlCommand(SQL, con);
                cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();
                cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier).Value = conversation.ConversationHandle;

                cmd.ExecuteNonQuery();
                this.conversation = null;

            }
            catch (SqlException ex)
            {
                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while ending conversation {0}. Timeout value was {1} seconds", this.conversation.ConversationHandle, timeout.TotalSeconds), ex);
                }
                else
                {
                    throw new CommunicationException(String.Format("An exception occurred while ending conversation {0}.", this.conversation.ConversationHandle), ex);
                }
            }
        }
    }
}
