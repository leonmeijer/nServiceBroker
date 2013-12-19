//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Diagnostics;
using System.IO;
namespace Microsoft.Samples.SsbTransportChannel
{
    internal class TraceHelper 
    {
        internal const string TraceRecordNs = "http://schemas.microsoft.com/2004/10/E2ETraceEvent/TraceRecord";
        internal const string TraceRecordName = "TraceRecord";
        internal const string TraceSourceName = "Microsoft.Samples.SsbTransportChannel";
        static TraceSource source;
        static TraceHelper()
        {
            source = new TraceSource(TraceHelper.TraceSourceName);
        }
        internal static void TraceEvent(TraceEventType eventType, string description, string methodName)
        {
            WriteTraceData(eventType, 0,  "", description, methodName, null);
        }
        internal static void TraceMethod(TraceEventType eventType,string methodName)
        {
            WriteTraceData(eventType, 0,  "", "Method executed",  methodName, null);
        }
        internal static void TraceMethod(string methodName)
        {
            WriteTraceData(TraceEventType.Verbose, 0, "", "Method executed", methodName, null);
        }
        private static void WriteTraceData(TraceEventType eventType, int id,  string traceIdentifier, string description,  string methodName, Exception exception)
        {
            if (source.Switch.ShouldTrace(eventType))
            {
                MemoryStream strm = new MemoryStream();
                XmlDictionaryWriter writer=XmlDictionaryWriter.CreateTextWriter(strm);
                writer.WriteStartElement(TraceRecordName);
                writer.WriteAttributeString("xmlns", TraceRecordNs);
                writer.WriteElementString("TraceIdentifier", traceIdentifier);
                writer.WriteElementString("Description", description);
                writer.WriteElementString("AppDomain", AppDomain.CurrentDomain.FriendlyName);
                writer.WriteElementString("MethodName", methodName);
                writer.WriteElementString("Source", TraceHelper.TraceSourceName);
                if (exception != null)
                {
                    WriteException(writer, exception);
                }
                writer.WriteEndElement();
                writer.Flush();
                strm.Position = 0;
                XPathDocument doc = new XPathDocument(strm);

                source.TraceData(eventType, id, doc.CreateNavigator());
                writer.Close();
                strm.Close();
            }
        }
        internal static void WriteException(XmlWriter writer, Exception exception)
        {
            if (exception != null)
            {
                writer.WriteStartElement("Exception");
                writer.WriteElementString("ExceptionType", exception.GetType().ToString());
                writer.WriteElementString("Message", exception.Message);
                writer.WriteElementString("StackTrace", exception.StackTrace);
                writer.WriteElementString("ExceptionString", exception.ToString());
                if (exception.InnerException != null)
                {
                    writer.WriteStartElement("InnerException");
                    WriteException(writer, exception.InnerException);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }
    }
}
