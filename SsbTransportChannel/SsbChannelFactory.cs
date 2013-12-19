//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Collections.ObjectModel;

namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbChannelFactory : ChannelFactoryBase<IOutputSessionChannel>
    {

        BufferManager bufferManager;
        MessageEncoderFactory messageEncoderFactory;
        string connstr;
        bool endConversationOnClose;
        string contract;
        bool useEncryption;
        bool useActionForSsbMessageType;
        Binding binding;


        public SsbChannelFactory(SsbBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            this.connstr = bindingElement.SqlConnectionString;
            this.bufferManager = BufferManager.CreateBufferManager(bindingElement.MaxBufferPoolSize, int.MaxValue);
            this.endConversationOnClose = bindingElement.SenderEndsConversationOnClose;
            this.useEncryption=bindingElement.UseEncryption;
            this.contract=bindingElement.Contract;
            this.useActionForSsbMessageType = bindingElement.UseActionForSsbMessageType;

            this.binding = context.Binding;

            Collection<MessageEncodingBindingElement> messageEncoderBindingElements = context.BindingParameters.FindAll<MessageEncodingBindingElement>();

            if (messageEncoderBindingElements.Count > 1)
            {
                throw new InvalidOperationException("More than one MessageEncodingBindingElement was found in the BindingParameters of the BindingContext");
            }
            else if (messageEncoderBindingElements.Count == 1)
            {
                this.messageEncoderFactory = messageEncoderBindingElements[0].CreateMessageEncoderFactory();
            }
            else
            {
                this.messageEncoderFactory = SsbConstants.DefaultMessageEncoderFactory;
            }
        }

        static Binding CreateResponseBinding(Binding binding, Guid ConversationGroupId)
        {

            BindingElementCollection bindingElements = binding.CreateBindingElements();
            bindingElements.Find<SsbBindingElement>().ConversationGroupId = ConversationGroupId;
            return new CustomBinding(bindingElements);
        }

        internal Binding Binding
        {
            get { return binding; }
        }


  

        protected override IOutputSessionChannel OnCreateChannel(System.ServiceModel.EndpointAddress remoteAddress, Uri via)
        {
            SsbOutputSessionChannel channel;
            channel = new SsbOutputSessionChannel(this, this.connstr, via, remoteAddress, this.bufferManager, this.messageEncoderFactory.Encoder, endConversationOnClose,contract,useEncryption,useActionForSsbMessageType );
            return channel;
        }
        protected override void OnOpen(TimeSpan timeout)
        {

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

    }
}
