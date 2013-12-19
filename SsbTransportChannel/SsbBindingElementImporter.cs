//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Globalization;
using System.Xml;
using WsdlNS = System.Web.Services.Description;

namespace Microsoft.Samples.SsbTransportChannel
{
    public class SsbBindingElementImporter : IPolicyImportExtension, IWsdlImportExtension
    {
        #region IWsdlImportExtension Members

        public void BeforeImport(System.Web.Services.Description.ServiceDescriptionCollection wsdlDocuments, System.Xml.Schema.XmlSchemaSet xmlSchemas, ICollection<XmlElement> policy)
        {
            
        }

        public void ImportContract(WsdlImporter importer, WsdlContractConversionContext context)
        {
            Console.WriteLine("ImportContract");
        }

        public void ImportEndpoint(WsdlImporter importer, WsdlEndpointConversionContext context)
        {
            Console.WriteLine("ImportEndpoint");
            
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Endpoint.Binding == null)
            {
                throw new ArgumentNullException("context.Endpoint.Binding");
            }

            BindingElementCollection bindingElements = context.Endpoint.Binding.CreateBindingElements();
            TransportBindingElement transportBindingElement = bindingElements.Find<TransportBindingElement>();
            if (transportBindingElement is SsbBindingElement)
            {
                ImportAddress(context);
            }
            if (context.Endpoint.Binding is CustomBinding)
            {
                Binding binding;

                if (transportBindingElement is SsbBindingElement)
                {
                    //if TryCreate is true, the CustomBinding will be replace by a SampleProfileUdpBinding in the
                    //generated config file for better typed generation.
                    if (SsbBinding.TryCreate(bindingElements, out binding))
                    {
                        binding.Name = context.Endpoint.Binding.Name;
                        binding.Namespace = context.Endpoint.Binding.Namespace;
                        context.Endpoint.Binding = binding;
                    }
                }
            }
        }

        //this imports the address of the endpoint.
        void ImportAddress(WsdlEndpointConversionContext context)
        {
            EndpointAddress address = null;

            if (context.WsdlPort != null)
            {
                XmlElement addressing10Element =
                    context.WsdlPort.Extensions.Find("EndpointReference", AddressingVersionConstants.WSAddressing10NameSpace);

                XmlElement addressing200408Element =
                    context.WsdlPort.Extensions.Find("EndpointReference", AddressingVersionConstants.WSAddressingAugust2004NameSpace);

                WsdlNS.SoapAddressBinding soapAddressBinding =
                    (WsdlNS.SoapAddressBinding)context.WsdlPort.Extensions.Find(typeof(WsdlNS.SoapAddressBinding));

                if (addressing10Element != null)
                {
                    address = EndpointAddress.ReadFrom(AddressingVersion.WSAddressing10,
                                                       new XmlNodeReader(addressing10Element));
                }
                if (addressing200408Element != null)
                {
                    address = EndpointAddress.ReadFrom(AddressingVersion.WSAddressingAugust2004,
                                                       new XmlNodeReader(addressing200408Element));
                }
                else if (soapAddressBinding != null)
                {
                    // checking for soapAddressBinding checks for both Soap 1.1 and Soap 1.2
                    address = new EndpointAddress(soapAddressBinding.Location);
                }
            }

            if (address != null)
            {
                context.Endpoint.Address = address;
            }
        }

        #endregion

        #region IPolicyImportExtension Members

        void IPolicyImportExtension.ImportPolicy(MetadataImporter importer, PolicyConversionContext context)
        {
            Console.WriteLine("ImportPolicy");
            if (importer == null)
            {
                throw new ArgumentNullException("importer");
            }
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            SsbBindingElement ssbBindingElement = null;
            PolicyAssertionCollection policyAssertions = context.GetBindingAssertions();
            if (policyAssertions.Remove(SsbConstants.SsbTransportAssertion, SsbConstants.SsbNs) != null)
            {
                ssbBindingElement = new SsbBindingElement();
                ssbBindingElement.SqlConnectionString = "";
            }
            if (ssbBindingElement != null)
            {
                context.BindingElements.Add(ssbBindingElement);
            }
        }

        #endregion
    }
}
