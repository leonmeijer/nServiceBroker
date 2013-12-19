//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Globalization;
using System.Xml;
using System.IO;
using WsdlNS = System.Web.Services.Description;
using System.Data.SqlClient;

namespace Microsoft.Samples.SsbTransportChannel
{
    public sealed class SsbBindingElement : TransportBindingElement, IPolicyExportExtension, IWsdlExportExtension
    {
        string sqlConnectionString;
        Guid? conversationGroupId = null;
        bool senderEndsConversationOnClose = false;
        string contract = "DEFAULT";
        bool useActionForSsbMessageType = false;

        bool useEncryption = false;
        static XmlDocument xmlDocument;




        public SsbBindingElement()
            : this(null,null)
        {
        }

        public SsbBindingElement(string sqlConnectionString)
            : this(sqlConnectionString,null)
        {
        }

        public SsbBindingElement(string sqlConnectionString, Guid conversationGroupId)
            : this(sqlConnectionString, (Guid?)conversationGroupId)
        {
        }


        SsbBindingElement(string sqlConnectionString, Guid? conversationGroupId)
            : base()
        {
            this.sqlConnectionString = sqlConnectionString;
            this.conversationGroupId = conversationGroupId;
            ValidateConnectionString(sqlConnectionString);

        }

        public SsbBindingElement(SsbBindingElement other)
            : base(other)
        {
            this.SqlConnectionString = other.SqlConnectionString;
            this.SenderEndsConversationOnClose = other.SenderEndsConversationOnClose;
            this.contract = other.contract;
            this.useEncryption = other.useEncryption;
            this.useActionForSsbMessageType = other.useActionForSsbMessageType;
            if (other.IsConversationGroupSpecified)
            {
                this.ConversationGroupId = other.ConversationGroupId;
            }
        }

        /// <summary>
        /// 
        /// Cache a list of valid connection strings.  There will typlically
        /// be only one, or perhaps two different connection strings for an AppDomain.
        /// So cache them to avoid the repetive expensive parsing using SqlConnectionStringBuilder
        /// </summary>
        static SortedList<string,string> validConnectionStrings = new SortedList<string,string>();

        /// <summary>
        /// Validate that the connection string meets the requirements to be used by the channel.
        /// In particular Asynchronous Processing and MultipleActiveResultSets are required.
        /// </summary>
        /// <param name="constr"></param>
        void ValidateConnectionString(string constr)
        {
            if (constr == null)
            {
                return;
            }
            if (validConnectionStrings.ContainsKey(constr))
            {
                return;
            }
            SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder(constr);
            if (!sb.AsynchronousProcessing)
            {
                throw new InvalidOperationException("SQL ConnectionString must have Asynchronous Processing=true");
            }
            if (!sb.MultipleActiveResultSets)
            {
                throw new InvalidOperationException("SQL ConnectionString must have MultipleActiveResultSets=true");
            }

            //arbitrary limit for cached validated connection strings.
            if (validConnectionStrings.Count < 100)
            {
                lock (validConnectionStrings)
                {
                    if (!validConnectionStrings.Keys.Contains(constr))
                    {
                        validConnectionStrings.Add(constr,constr);
                    }
                }
            }

        }


        /// <summary>
        /// Returns "net.ssb", the URI scheme for Service Broker.
        /// </summary>
        public override string Scheme
        {
            get { return SsbConstants.Scheme; }
        }


        /// <summary>
        /// The SqlConnection string used for the System.Data.SqlConnection connecting to the 
        /// SQL Server instance and database hosting the local endpoint for the Service Broker converdsation.
        /// 
        /// If the target service is in a remote databsae or a remote SQL Server instance, a Service Broker route must exist to the target.
        /// </summary>
        public string SqlConnectionString
        {
            get { return this.sqlConnectionString; }
            set 
            {
                ValidateConnectionString(value);
                this.sqlConnectionString = value; 
            }
        }

        /// <summary>
        /// The name of the Service Broker contract to use.  Defaults to 'default'.  
        /// </summary>
        public string Contract
        {
            get { return contract; }
            set 
            {
                contract = SsbHelper.ValidateIdentifier(value);
            }
        }


        /// <summary>
        /// Controls whether to use Service Broker encryption on the conversation.
        /// </summary>
        public bool UseEncryption
        {
            get { return useEncryption; }
            set { useEncryption = value; }
        }

        /// <summary>
        /// When using a Service Broker contract if the contract requires a message type other
        /// than DEFAULT, set the OperationContract's Action to the required Ssb Message type
        /// and set the UseActionForSsbMessageType flag on the binding.
        /// </summary>
        public bool UseActionForSsbMessageType
        {
            get { return useActionForSsbMessageType; }
            set { useActionForSsbMessageType = value; }
        }
        



        /// <summary>
        /// If set to true this property instructs the SsbOutputChannel to send the EndConversation message
        /// in its OnClose method.  If set to true, it will not be possible to get response messages on the conversation
        /// or send additional messages on the conversation.  However if this property is not set, then you must
        /// end the conversation explicitly at some time in the future, either sua sponte or in response to an End Conversatoin
        /// message from the other end of the conversation.
        /// 
        /// As an alternative, you can place an activiated stored procedure on the local end of the queue to process
        /// responses or end conversation messages.
        /// 
        /// The default value of SenderEndsConversationOnClose is false.
        /// </summary>
        public bool SenderEndsConversationOnClose
        {
            get { return senderEndsConversationOnClose; }
            set { senderEndsConversationOnClose = value; }
        }
        

  
        /// TODO (dbrowne) Evaluate whether we need conversation binding.  Without it, waiting for a reply on a 
        ///specific conversation in a conversation group may return messages in a related conversation.
        /// 
        /// To support SsbConversationGroupSender and Reciever would need parallel implementations of some methods. 
        /// And if it's really needed it will be easier to add later after everything else is nailed down, and we
        /// won't have to evolve the parallel code paths together.
        /// 
        /// 
        /// 
        ///


        
        //public bool IsConversationHandleSpecified
        //{
        //    get { return conversationHandle.HasValue; }
        //}


        //public Guid ConversationHandle
        //{
        //    get
        //    {
        //        if (!conversationHandle.HasValue)
        //        {
        //            throw new InvalidOperationException("Conversation Group ID not specified");
        //        }
        //        return conversationHandle.Value;
        //    }
        //    set { conversationHandle = value; }
        //}

        /// <summary>
        /// Indicates whether a Channel Listener should be restricted to a particular conversation group.  This is useful
        /// for waiting for response messages on a conversation.
        /// </summary>
      public bool IsConversationGroupSpecified
        {
            get { return conversationGroupId.HasValue; }
        }
        public Guid ConversationGroupId
        {
            get
            {
                if (!conversationGroupId.HasValue)
                {
                    throw new InvalidOperationException("Conversation Group ID not specified");
                }
                return conversationGroupId.Value;
            }
            set
            {
                conversationGroupId = value;
            }

        }

        public override BindingElement Clone()
        {
            return new SsbBindingElement(this);
        }
        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            return (IChannelFactory<TChannel>)(object)new SsbChannelFactory(this, context);
        }
        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (!this.CanBuildChannelListener<TChannel>(context))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Unsupported channel type: {0}.", typeof(TChannel).Name));
            }

            return (IChannelListener<TChannel>)(object)new SsbChannelListener(this, context);
        }
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            return (typeof(TChannel) == typeof(IOutputSessionChannel));
        }
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            return (typeof(TChannel) == typeof(IInputSessionChannel));
        }


        #region IPolicyExportExtension Members

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
            {
                throw new ArgumentNullException("exporter");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ICollection<XmlElement> bindingAssertions = context.GetBindingAssertions();
            XmlDocument xmlDocument = new XmlDocument();
            bindingAssertions.Add(xmlDocument.CreateElement(
                SsbConstants.SsbNsPrefix, SsbConstants.SsbTransportAssertion, SsbConstants.SsbNs));

            bool createdNew = false;
            MessageEncodingBindingElement encodingBindingElement = context.BindingElements.Find<MessageEncodingBindingElement>();
            if (encodingBindingElement == null)
            {
                createdNew = true;
                encodingBindingElement = SsbConstants.DefaultMessageEncodingBindingElement;
            }

            if (createdNew && encodingBindingElement is IPolicyExportExtension)
            {
                ((IPolicyExportExtension)encodingBindingElement).ExportPolicy(exporter, context);
            }

            AddWSAddressingAssertion(context, encodingBindingElement.MessageVersion.Addressing);
        }

        #endregion

        
        static void AddWSAddressingAssertion(PolicyConversionContext context, AddressingVersion addressing)
        {
            XmlElement addressingAssertion = null;

            if (addressing == AddressingVersion.WSAddressing10)
            {
                addressingAssertion = XmlDoc.CreateElement("wsaw", "UsingAddressing", "http://www.w3.org/2006/05/addressing/wsdl");
            }
            else if (addressing == AddressingVersion.WSAddressingAugust2004)
            {
                addressingAssertion = XmlDoc.CreateElement("wsap", "UsingAddressing", AddressingVersionConstants.WSAddressingAugust2004NameSpace + "/policy");
            }
            else if (addressing == AddressingVersion.None)
            {
                // do nothing
                addressingAssertion = null;
            }
            else
            {
                throw new InvalidOperationException("This addressing version is not supported:\n" + addressing.ToString());
            }

            if (addressingAssertion != null)
            {
                context.GetBindingAssertions().Add(addressingAssertion);
            }
        }

        

        #region IWsdlExportExtension Members

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext context)
        {
            
        }

        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context)
        {
            BindingElementCollection bindingElements = context.Endpoint.Binding.CreateBindingElements();
            MessageEncodingBindingElement encodingBindingElement = bindingElements.Find<MessageEncodingBindingElement>();

            if (encodingBindingElement == null)
            {
                encodingBindingElement = SsbConstants.DefaultMessageEncodingBindingElement;
            }

            // Set SoapBinding Transport URI
            WsdlNS.SoapBinding soapBinding = GetSoapBinding(context, exporter);

            if (soapBinding != null)
            {
                soapBinding.Transport = SsbConstants.SsbNs;
            }
            if (context.WsdlPort != null)
            {
                AddAddressToWsdlPort(context.WsdlPort, context.Endpoint.Address, encodingBindingElement.MessageVersion.Addressing);
            }
        }

        #endregion

        private static WsdlNS.SoapBinding GetSoapBinding(WsdlEndpointConversionContext endpointContext, WsdlExporter exporter)
        {
            EnvelopeVersion envelopeVersion = null;
            WsdlNS.SoapBinding existingSoapBinding = null;
            object versions = null;
            object SoapVersionStateKey = new object();

            //get the soap version state
            if (exporter.State.TryGetValue(SoapVersionStateKey, out versions))
            {
                Dictionary<WsdlNS.Binding, EnvelopeVersion> vd = (Dictionary<WsdlNS.Binding, EnvelopeVersion>)versions;
                if (versions != null && vd.ContainsKey(endpointContext.WsdlBinding))
                {
                    envelopeVersion = vd[endpointContext.WsdlBinding];
                }
            }

            if (envelopeVersion == EnvelopeVersion.None)
            {
                return null;
            }

            //get existing soap binding
            foreach (object o in endpointContext.WsdlBinding.Extensions)
            {
                if (o is WsdlNS.SoapBinding)
                {
                    existingSoapBinding = (WsdlNS.SoapBinding)o;
                }
            }

            return existingSoapBinding;
        }
        private static void AddAddressToWsdlPort(WsdlNS.Port wsdlPort, EndpointAddress endpointAddress, AddressingVersion addressing)
        {
            if (addressing == AddressingVersion.None)
            {
                return;
            }

            MemoryStream memoryStream = new MemoryStream();
            XmlWriter xmlWriter = XmlWriter.Create(memoryStream);
            xmlWriter.WriteStartElement("temp");

            if (addressing == AddressingVersion.WSAddressing10)
            {
                xmlWriter.WriteAttributeString("xmlns", "wsa10", null, AddressingVersionConstants.WSAddressing10NameSpace);
            }
            else if (addressing == AddressingVersion.WSAddressingAugust2004)
            {
                xmlWriter.WriteAttributeString("xmlns", "wsa", null, AddressingVersionConstants.WSAddressingAugust2004NameSpace);
            }
            else
            {
                throw new InvalidOperationException("This addressing version is not supported:\n" + addressing.ToString());
            }

            endpointAddress.WriteTo(addressing, xmlWriter);
            xmlWriter.WriteEndElement();

            xmlWriter.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);

            XmlReader xmlReader = XmlReader.Create(memoryStream);
            xmlReader.MoveToContent();

               

            XmlElement endpointReference = (XmlElement)XmlDoc.ReadNode(xmlReader).ChildNodes[0];

            wsdlPort.Extensions.Add(endpointReference);
        }
        //reflects the structure of the wsdl
        static XmlDocument XmlDoc
        {
            get
            {
                if (xmlDocument == null)
                {
                    NameTable nameTable = new NameTable();
                    nameTable.Add("Policy");
                    nameTable.Add("All");
                    nameTable.Add("ExactlyOne");
                    nameTable.Add("PolicyURIs");
                    nameTable.Add("Id");
                    nameTable.Add("UsingAddressing");
                    nameTable.Add("UsingAddressing");
                    xmlDocument = new XmlDocument(nameTable);
                }
                return xmlDocument;
            }
        }
    }
}
