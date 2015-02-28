using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace TcpFramework.Server
{
    internal class ServerSession
    {
        internal EndPoint RemoteEndPoint;
        internal int SessionId;
        internal int ReceiveTransmissionId;
    }
}
