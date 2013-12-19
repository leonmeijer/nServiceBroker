//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Runtime.Remoting.Messaging;
using System.Xml;

namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbInputSessionChannel : ChannelBase, IInputSessionChannel
    {
        SsbConversationGroupReceiver cg;
        MessageEncoder encoder;
        BufferManager bufferManager;
        Uri listenAddress;
        SsbChannelListener listener;


        //Message queuedMessage;

        public SsbInputSessionChannel(SsbChannelListener listener, SsbConversationGroupReceiver cg, BufferManager bufferManager, MessageEncoder encoder, Uri listenAddress)
            : base(listener)
        {
            this.cg = cg;
            this.listenAddress = listenAddress;
            this.bufferManager = bufferManager;
            this.encoder = encoder;
            this.listener = listener;
        }

        #region IInputChannel Members


        public Message Receive(TimeSpan timeout)
        {

            TraceHelper.TraceMethod("SsbInputSessionChannel.Receive");
            ThrowIfDisposedOrNotOpen();

            byte[] buffer = null;
            SsbConversationContext conversation;
            int messageLength;

            //Don't linger waiting for messages.  When the last message is received, go ahead and close the channel.
            //A new channel will be created if additional messages arive later on this conversation.  To maintain
            //state between messages in a long-running conversation use a custom instance provider and/or save
            //the state to a database keyed by the ConversationGroup
            // TODO (dbrowne) consider allowing linger except after a conversation has ended as a performance enhancement.
            buffer = this.cg.Receive(TimeSpan.Zero, this.bufferManager, out messageLength, out conversation);
            TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "SsbConversationGroupReceiver.Receiver returned", "SsbInputSessionChannel.Receive");

            if (conversation != null && conversation.MessageTypeName == SsbConstants.SsbEndDialogMessage)
            {
                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "EndDialogMessage received, ending conversation", "SsbInputSessionChannel.Receive");
                //End the conversation on our end.  There's nothing else we can can do, so we might as well end the conversation 
                //on our end, so when we commit, the conversation will be cleaned up.
                cg.EndConversation(conversation.ConversationHandle, this.DefaultSendTimeout);
                return null;
            }
            // CONSIDER (yassers) raising events for first and last message in conversation
            else if (buffer == null)
            {
                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "SsbConversationGroupReceiver.Receiver returned null, no messages available", "SsbInputSessionChannel.Receive");
                return null; //there are no more messages available
            }

            Message message = this.encoder.ReadMessage(new ArraySegment<byte>(buffer, 0, messageLength), this.bufferManager);
            TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "Decoded message", "SsbInputSessionChannel.Receive");
            message.Properties.Add(SsbConstants.SsbConversationMessageProperty, conversation);
            //since we received the message it must be coming from the right queue
            //the To header isn't going to match the address that the Endpoint filter is looking for
            //so we fix that here
            message.Headers.To = this.listenAddress;
            TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Verbose, "Returning message", "SsbInputSessionChannel.Receive");
            return message;

        }

        public Message Receive()
        {
            return this.Receive(TimeSpan.MaxValue);
        }


        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            message = null;
            try
            {
                message = this.Receive(timeout);
                if (message == null)
                {
                    return true;
                }

                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
        public bool WaitForMessage(TimeSpan timeout)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(SsbConversationGroupReceiver))
            {
                return ((T)(object)this.cg);
            }

            return base.GetProperty<T>();
        }
        #endregion

        #region ISessionChannel<IInputSession> Members

        public IInputSession Session
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion

        protected override void OnAbort()
        {
            this.cg.Abort();
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

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            ReceiveDelegate d = new ReceiveDelegate(this.Receive);
            return d.BeginInvoke(timeout, callback, state);
        }

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return this.BeginReceive(TimeSpan.MaxValue, callback, state);
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return this.BeginReceive(TimeSpan.MaxValue, callback, state);
        }

        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public Message EndReceive(IAsyncResult result)
        {

            System.Runtime.Remoting.Messaging.AsyncResult aresult = result as System.Runtime.Remoting.Messaging.AsyncResult;
            if (aresult == null)
            {
                throw new ArgumentException("Invalid IAsyncResult type");
            }
            ReceiveDelegate d = aresult.AsyncDelegate as ReceiveDelegate;
            return d.EndInvoke(result);
        }

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            message = null;
            try
            {
                message = this.EndReceive(result);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool EndWaitForMessage(IAsyncResult result)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        #endregion


        protected override void OnClose(TimeSpan timeout)
        {
            this.cg.Close();
        }
        protected override void OnOpen(TimeSpan timeout)
        {
            if (this.cg.State == CommunicationState.Opened)
            {
                return; //already open
            }
            this.cg.Open();
        }
        public EndpointAddress LocalAddress
        {
            get { return new EndpointAddress(this.listenAddress); }
        }

    }

}
