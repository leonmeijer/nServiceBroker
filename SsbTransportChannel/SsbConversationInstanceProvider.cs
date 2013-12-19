//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;
using System.ServiceModel;
using System.Data.SqlClient;
using System.Transactions;

namespace Microsoft.Samples.SsbTransportChannel
{
    /// <summary>
    /// The Instance Provider supports servicees using the service instance to maintain the state
    /// of long-running conversations.  This adds onto the session-level guarantee that all messages
    /// in a session will be processed by the same service instance.  Since a long-running Service Broker
    /// conversation will span a number of WCF sessions, the service instance provides a convienent scope
    /// for storing conversation-releated state.
    /// 
    /// To use this class create a concrete subclass of SsbConversationInstanceProvider for your service and register
    /// it as a service endpoint behavior.  Then when a message arives WCF will call your implementaiton of GetInstance
    /// to create or retrieve a service instance.  When the session is done, WCF will call your implementation of 
    /// ReleaseInstance where you can save the service instance's state to a database.  
    /// 
    /// For the duration of the WCF session (which is typically quite short), the SsbInputSessionChannel will
    /// maintain an open database connection and a Transaction.  You can access this transaction and/or the connection 
    /// when loading or saving a service instance with
    /// 
    /// SsbConversationGroupReceiver receiver = OperationContext.Current.Channel.GetProperty<SsbConversationGroupReceiver>();
    /// 
    /// For the duration of the session WCF will own a lock on the conversation group in SQL Server, preventing concurrent calls to GetInstance
    /// by different callers.
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    public abstract class SsbConversationInstanceProvider<TService> : IInstanceProvider, IEndpointBehavior
    {

        protected abstract TService GetInstance(SsbConversationContext conversationContext, SqlConnection con);

        protected abstract void ReleaseInstance(Guid conversationGroupId, TService instance, SqlConnection con);

        #region IInstanceProvider Members

        public object GetInstance(System.ServiceModel.InstanceContext instanceContext, System.ServiceModel.Channels.Message message)
        {
            SsbConversationContext conversationContext = (SsbConversationContext)OperationContext.Current.IncomingMessageProperties[SsbConstants.SsbConversationMessageProperty];
            SsbConversationGroupReceiver cgr = OperationContext.Current.Channel.GetProperty<SsbConversationGroupReceiver>();
            SqlConnection con = cgr.GetConnection();
            Guid conversationGroupID = cgr.ConversationGroupId;
            TService instance = (TService)GetInstance(conversationContext, con);

            cgr.Closing += delegate(object sender, EventArgs e)
            {
                //SsbConversationGroupReceiver cgr = (SsbConversationGroupReceiver)sender;
                ReleaseInstance(conversationGroupID, instance, con);
            };
            return instance;
        }


        public object GetInstance(System.ServiceModel.InstanceContext instanceContext)
        {
            SsbConversationContext conversationContext = (SsbConversationContext)OperationContext.Current.IncomingMessageProperties[SsbConstants.SsbConversationMessageProperty];
            SqlConnection con = OperationContext.Current.Channel.GetProperty<SsbConversationSender>().GetConnection();
            return GetInstance(conversationContext,con); 
        }

        /// <summary>
        /// WCF only calls ReleaseInstance _after_ the channel is closed.  This is too late for us because
        /// we need to have the option to persist the service instance in the scope of the transaction that
        /// the input channel uses.  If we wait unitl after, another instance of the service might request this instance
        /// before we're done saving it.  GetInstance installs an event handler onto the SsbConversationGroupReciever's 
        /// Closing Event to call ReleaseInstance(Guid conversationGroupId, TService instance, SqlConnection con);
        /// </summary>
        /// <param name="instanceContext"></param>
        /// <param name="instance"></param>
        public void ReleaseInstance(System.ServiceModel.InstanceContext instanceContext, object instance)
        {
            ;//NOOP this is already done by the CGR closing event
        }

        #endregion

        #region IEndpointBehavior Members

        void IEndpointBehavior.AddBindingParameters(ServiceEndpoint endpoint, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
            return;
        }

        void IEndpointBehavior.ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            return;
        }

        void IEndpointBehavior.ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.InstanceProvider = this;
        }

        void IEndpointBehavior.Validate(ServiceEndpoint endpoint)
        {
            return;
        }

        #endregion
    }
}
