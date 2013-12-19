//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Microsoft.Samples.SsbTransportChannel
{
    [DataContract]
    public class SsbConversationContext
    {
        private Guid coversationHandle;
        private Guid conversationGroupId;
        private long messageSequenceNumber;
        private string messageTypeName;
        internal SsbConversationContext(Guid conversationHandle, Guid conversationGroupId,long sequence,string messageTypeName)
        {
            this.coversationHandle = conversationHandle;
            this.conversationGroupId = conversationGroupId;
            this.messageSequenceNumber = sequence;
            this.messageTypeName = messageTypeName;

        }
        [DataMember]
        public Guid ConversationHandle
        {
            get { return coversationHandle; }
        }
        [DataMember]
        public Guid ConversationGroupId
        {
            get { return conversationGroupId; }
        }
        [DataMember]
        public long MessageSequenceNumber
        {
            get { return messageSequenceNumber; }
        }
        [DataMember]
        public string MessageTypeName
        {
            get { return messageTypeName; }
        }
    }
}
