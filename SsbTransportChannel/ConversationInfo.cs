//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Samples.SsbTransportChannel
{
    class ConversationInfo
    { 
        internal string ServiceName;
        internal string QueueName;
        internal string TargetServiceName;
        internal Guid ConversationID;
        internal Guid ConversationHandle;
        internal Guid ConversationGroupID;
        internal string State;

        internal ConversationInfo(
            string serviceName, 
            string queueName, 
            string targetServiceName,
            Guid conversationID, 
            Guid conversationHandle, 
            Guid conversationGroupID,
            string state)
        {
            this.ServiceName = serviceName;
            this.QueueName = queueName;
            this.TargetServiceName = targetServiceName;
            this.ConversationID = conversationID;
            this.ConversationHandle = conversationHandle;
            this.ConversationGroupID = conversationGroupID;
            this.State = state;
        }

        internal string StateDescription
        {
            get
            {
                return TransmissionStates[State];
            }
        }

        internal static string GetTransmissionStateDescription(string code)
        {
            return TransmissionStates[code];
        }

        static readonly Dictionary<string, string> TransmissionStates = LoadTransmissionStates();
        static Dictionary<string, string> LoadTransmissionStates()
        {
            Dictionary<string, string> states = new Dictionary<string, string>();
            states.Add("SO", "Started Outbound");
            states.Add("SI", "Started Inbound");
            states.Add("CO", "Conversing");
            states.Add("DI", "Disconnected");
            states.Add("DO", "Disconnected Outbound");
            states.Add("ER", "Error");
            states.Add("CD", "Closed");
            return states;
        }


    }
}
