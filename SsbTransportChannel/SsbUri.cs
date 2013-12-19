//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Microsoft.Samples.SsbTransportChannel
{
    internal class SsbUri
    {
        //net.ssb:source=client1:target=service1

        Uri uri;
        String source;
        String target;
        //Guid? conversationGroup;
        //Guid? conversation;

        //public Uri Reverse()
        //{
        //    return new Uri(string.Format("net.ssb:source={0}:target={1}", target, source));
        //}

        public SsbUri(Uri uri)
        {

 
            // URI schemes are case-insensitive, so try a case insensitive compare now
            if (string.Compare(uri.Scheme, SsbConstants.Scheme, true, System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    "Invalid URI scheme: {0}.  Must be {1}.", uri.Scheme, SsbConstants.Scheme), "baseAddress");
            }
            string[] segments = uri.LocalPath.Split(new char[] { '=', ':' });
            if (segments.Length != 4)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    "Invalid URI. Must be net.ssb:source=...:target=..."));

            }
            this.uri = uri;
            this.source = segments[1];
            this.target = segments[3];
        }

        public Uri Uri
        {
            get { return this.uri; }
        }

        public override string ToString()
        {
            return uri.ToString();
        }
  

        public String Service
        {
            get { return this.target; }
        }
        public String Client
        {
            get { return this.source; }
        }
        internal static Uri Default = new Uri("net.ssb:source=*:target=*");
    }
}
