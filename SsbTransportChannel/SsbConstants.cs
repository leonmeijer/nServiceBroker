//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel.Channels;

namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbConstants
    {
        public const string Scheme = "net.ssb";
        public static readonly MessageEncoderFactory DefaultMessageEncoderFactory = new TextMessageEncodingBindingElement().CreateMessageEncoderFactory();
        public static readonly MessageEncodingBindingElement DefaultMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
        public static readonly TimeSpan DefaultOpenTimeout = new TimeSpan(0, 2, 0);
        public static readonly TimeSpan DefaultCloseTimeout = new TimeSpan(0, 2, 0);
        public const string SsbConversationMessageProperty = "SsbConversationMessageProperty";
        public const string FirstReceivedMessageInConversationAction = "FirstReceivedMessageInConversationAction";
        public const string ConversationTimerAction = "ConversationTimerAction";
        public const string EndConversationAction = "EndConversationAction";
        public const string OnFirstReceivedMessageInConversationElementName = "OnFirstReceivedMessageInConversation";
        public const string OnConversationTimerElementName = "OnConversationTimer";
        public const string OnEndConversationElementName = "OnEndConversation";
        public const string SsbNs="urn:net.ssb:SsbTransport";
        public const string SsbNsPrefix = "ssb";
        public const string SsbTransportAssertion = "net.ssb";
        public const string SsbEndDialogMessage = "http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog";
        public const string SsbErrorMessage="http://schemas.microsoft.com/SQL/ServiceBroker/Error";
        public const string SsbDialogTimerMessage = "http://schemas.microsoft.com/SQL/ServiceBroker/DialogTimer";
    }
    public class SsbConfigurationStrings
    {
        public const string SenderEndsConversationOnCloseProperty = "senderEndsConversationOnClose";
        public const string SqlConnectionStringProperty = "sqlConnectionString";
        public const string SsbBindingSectionName = "system.serviceModel/bindings/ssbBinding";
    }
    static class AddressingVersionConstants
    {
        internal const string WSAddressing10NameSpace = "http://www.w3.org/2005/08/addressing";
        internal const string WSAddressingAugust2004NameSpace = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
    }
}
