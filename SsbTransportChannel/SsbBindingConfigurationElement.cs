//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.ServiceModel;
using System.ServiceModel.Configuration;
using System.ServiceModel.Channels;
using System.Configuration;


namespace Microsoft.Samples.SsbTransportChannel
{
    public sealed class SsbBindingConfigurationElement
        : StandardBindingElement
    {
        public SsbBindingConfigurationElement()
            : this(null)
        {
        }

        public SsbBindingConfigurationElement(string configurationName)
            : base(configurationName)
        {
        }

        protected override Type BindingElementType
        {
            get { return typeof(SsbBinding); }
        }

        [ConfigurationProperty(SsbConfigurationStrings.SenderEndsConversationOnCloseProperty)]
        public bool SenderEndsConversationOnClose
        {
            get
            {
                if (base[SsbConfigurationStrings.SenderEndsConversationOnCloseProperty] == null)
                {
                    return false;
                }
                else
                {
                    return (bool)base[SsbConfigurationStrings.SenderEndsConversationOnCloseProperty];
                }

            }
            set
            {
                base[SsbConfigurationStrings.SenderEndsConversationOnCloseProperty] = value;
            }
        }

        [ConfigurationProperty(SsbConfigurationStrings.SqlConnectionStringProperty)]
        public string SqlConnectionString
        {
            get
            {
                return (string)base[SsbConfigurationStrings.SqlConnectionStringProperty];
            }
            set
            {
                base[SsbConfigurationStrings.SqlConnectionStringProperty] = value;
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                base.Properties.Add(
                        new ConfigurationProperty(
                            SsbConfigurationStrings.SenderEndsConversationOnCloseProperty,
                            typeof(bool),null));

                base.Properties.Add(
                        new ConfigurationProperty(
                            SsbConfigurationStrings.SqlConnectionStringProperty,
                            typeof(string),null));

                return base.Properties;
            }
        }

        protected override void InitializeFrom(Binding binding)
        {
            base.InitializeFrom(binding);

            SsbBinding ssbBinding = (SsbBinding)binding;
            this.SqlConnectionString = ssbBinding.SqlConnectionString;
            this.SenderEndsConversationOnClose = ssbBinding.SenderEndsConversationOnClose;
        }

        protected override void OnApplyConfiguration(Binding binding)
        {
            SsbBinding ssbBinding = (SsbBinding)binding;
            ssbBinding.SenderEndsConversationOnClose = this.SenderEndsConversationOnClose;
            ssbBinding.SqlConnectionString = this.SqlConnectionString;
        }
        protected override bool SerializeElement(System.Xml.XmlWriter writer, bool serializeCollectionKey)
        {
            return base.SerializeElement(writer, serializeCollectionKey);
        }
        protected override bool SerializeToXmlElement(System.Xml.XmlWriter writer, string elementName)
        {
            return base.SerializeToXmlElement(writer, elementName);
        }
    }
}
