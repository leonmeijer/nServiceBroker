//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Microsoft.Samples.SsbTransportChannel
{
    internal class SsbSession : IOutputSession, IInputSession
    {
        string id;
        public SsbSession()
        {
            this.id = Guid.NewGuid().ToString();
        }
        #region ISession Members

        public string Id
        {
            get { return this.id; }
        }

        #endregion

    
    }
}
