//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Configuration;

namespace Microsoft.Samples.SsbTransportChannel
{

    public class SsbBinding : Binding, IBindingRuntimePreferences, IBindingDeliveryCapabilities
    {
        SsbBindingElement ssbbe;
        TextMessageEncodingBindingElement encbe;
        public SsbBinding()
            : base()
        {
            Initialize();
        }
        public SsbBinding(string name, string ns)
            : base(name, ns)
        {
            Initialize();
        }
        public SsbBinding(string configurationName) 
            : this()
        {
            ApplyConfiguration(configurationName);
        }
        public override BindingElementCollection CreateBindingElements()
        {
            BindingElementCollection elements = new BindingElementCollection();
            elements.Add(encbe);
            elements.Add(ssbbe);
            return elements;
        }

        public override string Scheme
        {
            get { return SsbConstants.Scheme; }
        }
        public bool SenderEndsConversationOnClose
        {
            get { return ssbbe.SenderEndsConversationOnClose; }
            set { ssbbe.SenderEndsConversationOnClose = value; }
        }

        public string Contract
        {
            get { return ssbbe.Contract; }
            set { ssbbe.Contract = value; }
        }

        public bool UseEncryption
        {
            get { return ssbbe.UseEncryption; }
            set { ssbbe.UseEncryption = value; }
        }


        public bool UseActionForSsbMessageType
        {
            get { return ssbbe.UseActionForSsbMessageType ; }
            set { ssbbe.UseActionForSsbMessageType = value; }
        }
        
        public string SqlConnectionString
        {
            get { return ssbbe.SqlConnectionString; }
            set { this.ssbbe.SqlConnectionString = value; }
        }
        
        void Initialize()
        {
            this.ssbbe = new SsbBindingElement();
            this.encbe = new TextMessageEncodingBindingElement();
        }
        void InitializeFrom(SsbBindingElement ssbBindingElement, TextMessageEncodingBindingElement textMessageEncodingBindingElement)
        {
            this.ssbbe = ssbBindingElement;
            this.encbe = textMessageEncodingBindingElement;
        }
        void ApplyConfiguration(string configurationName)
        {
            SsbBindingCollectionElement section =
                (SsbBindingCollectionElement)ConfigurationManager.GetSection(
                    SsbConfigurationStrings.SsbBindingSectionName);

            SsbBindingConfigurationElement element = section.Bindings[configurationName];
            element.ApplyConfiguration(this);
        }
        #region IBindingRuntimePreferences Members

        public bool ReceiveSynchronously
        {
            get { return true; }
        }

        #endregion

        #region IBindingDeliveryCapabilities Members

        public bool AssuresOrderedDelivery
        {
            get { return true; }
        }

        public bool QueuedDelivery
        {
            get { return true; }
        }

        #endregion


        public static bool TryCreate(BindingElementCollection elements, out Binding binding)
        {
            binding = null;
            if (elements.Count > 4)
            {
                return false;
            }

            TextMessageEncodingBindingElement textMessageEncodingBindingElement = null;
            SsbBindingElement ssbBindingElement = null;

            foreach (BindingElement element in elements)
            {
                if (element is TextMessageEncodingBindingElement)
                {
                    textMessageEncodingBindingElement = (TextMessageEncodingBindingElement)element;
                }
                else if (element is SsbBindingElement)
                {
                    ssbBindingElement = (SsbBindingElement)element;
                }
                else
                {
                    return false;
                }
            }

            if (ssbBindingElement == null || textMessageEncodingBindingElement==null)
            {
                return false;
            }

            SsbBinding ssbbinding = new SsbBinding();
            ssbbinding.InitializeFrom(ssbBindingElement, textMessageEncodingBindingElement);
            binding = ssbbinding;
            return true;
        }
    }
}
