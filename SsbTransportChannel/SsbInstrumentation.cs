//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.Samples.SsbTransportChannel
{
    internal static class SsbInstrumentation
    {
        static int messagesSent = 0;
        static int bytesSent = 0;
        static int messagesReceived = 0;
        static int bytesRecieved = 0;

        internal static void MessageSent(int messageSize)
        {
            Interlocked.Add(ref bytesSent, messageSize);
            Interlocked.Increment(ref messagesSent);            
        }

        internal static void MessageRecieved(int messageSize)
        {
            Interlocked.Add(ref bytesRecieved, messageSize);
            Interlocked.Increment(ref messagesReceived);
        }


    }
}
