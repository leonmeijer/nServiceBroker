using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;

namespace SimpleSsbTransport
{
    [ServiceContract(Namespace = SsbConstants.SsbNs)]
    public interface ISsbReceiverContract
    {
        [OperationContract(IsOneWay = true,Action=SsbConstants.FirstReceivedMessageInConversationAction)]
        void OnFirstReceivedMessageInConversation();


        [OperationContract(IsOneWay = true, Action = SsbConstants.ConversationTimerAction)]
        void OnConversationTimer();

        [OperationContract(IsOneWay = true,Action=SsbConstants.EndConversationAction)]
        void OnEndConversation();

    }
}
