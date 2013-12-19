//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using System.Data.SqlClient;
namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbOutputSessionChannel : ChannelBase, IOutputSessionChannel
    {
        SsbConversationSender sender;
        Uri via;
        //Uri sourceAddress;
        //EndpointAddress replyTo; 
        EndpointAddress to;
        SsbSession session;
        BufferManager bufferManager;
        MessageEncoder encoder;
        string connectionString;
        string contract;
        bool useEncryption;
        bool useActionForSsbMessageType;

        SsbChannelFactory factory;
        public SsbOutputSessionChannel(SsbChannelFactory factory, string connectionString, Uri via,EndpointAddress remoteAddress, BufferManager buffermanager, MessageEncoder encoder, bool endConversationOnClose, string contract, bool useEncryption, bool useActionForSsbMessageType)
            : base(factory)
        {
            if (via == null)
            {
                throw new ArgumentNullException("via");
            }
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (encoder == null)
            {
                throw new ArgumentNullException("encoder");
            }
            if (buffermanager == null)
            {
                throw new ArgumentNullException("bufferManager");
            }
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("connectionString");
            }
            this.factory = factory;
            this.via = via;
            this.to = remoteAddress;
            this.session = new SsbSession();
            this.encoder = encoder;
            this.bufferManager = buffermanager;
            this.connectionString = connectionString;
            this.contract = contract;
            this.useEncryption = useEncryption;
            this.useActionForSsbMessageType = useActionForSsbMessageType;
            
            this.sender = new SsbConversationSender(connectionString, via, factory, endConversationOnClose,contract,useEncryption );
        }
        #region IOutputChannel Members


 
        public EndpointAddress RemoteAddress
        {
            get { return this.to; }
        }

        public void Send(Message message, TimeSpan timeout)
        {
            ThrowIfDisposedOrNotOpen(); //now must be Opened

            string messageType = "DEFAULT";
            if (this.useActionForSsbMessageType )
            {
                messageType = message.Headers.Action;
            }
            if (this.to != null)
            {
                this.to.ApplyTo(message);
            }
            ArraySegment<byte> messagebuffer = this.encoder.WriteMessage(message, Int32.MaxValue, bufferManager);
            message.Close();
            byte[] buffer = new byte[messagebuffer.Count];
            Buffer.BlockCopy(messagebuffer.Array, messagebuffer.Offset, buffer, 0, messagebuffer.Count);

            this.sender.Send(buffer, timeout, messageType);
            bufferManager.ReturnBuffer(messagebuffer.Array);
        }

        public void Send(Message message)
        {
            this.Send(message, TimeSpan.MaxValue);
        }

        public Uri Via
        {
            get { return this.via; }
        }

        #endregion

 
        public IOutputSession Session
        {
            get { return this.session; }
        }

        protected override void OnAbort()
        {
            sender.Abort();
        }

        protected override void OnClose(TimeSpan timeout)
        {
            sender.Close();

        }

        protected override void OnOpen(TimeSpan timeout)
        {

    
            sender.Open();
        }
        #region AsyncAWrappers

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void EndSend(IAsyncResult result)
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

        public override T GetProperty<T>()
        {
            if (typeof(T)==typeof(SsbConversationSender))
            {
                return ((T)(object)this.sender);
            }
            return base.GetProperty<T>();
        }
        //private void SetConversation(Message message)
        //{
        //    XmlReader reader = message.GetReaderAtBodyContents();
        //    while (reader.Read())
        //    {
        //        if (reader.NodeType == XmlNodeType.Element &&
        //            reader.Name == SsbConstants.ConversationHandleElementName &&
        //            reader.NamespaceURI == SsbConstants.SsbContractNs)
        //        {
        //            string t = reader.ReadElementString();
        //            if (!String.IsNullOrEmpty(t))
        //            {
        //                this.cid = new Guid(t);
        //            }
        //        }
        //        else if (reader.NodeType == XmlNodeType.Element &&
        //            reader.Name == SsbConstants.ConversationGroupHandleElementName &&
        //            reader.NamespaceURI == SsbConstants.SsbContractNs)
        //        {
        //            string t = reader.ReadElementString();
        //            if (!String.IsNullOrEmpty(t))
        //            {
        //                this.cgid = new Guid(t);
        //            }
        //        }
        //    }
        //    reader.Close();

        //}
        
    }
}
