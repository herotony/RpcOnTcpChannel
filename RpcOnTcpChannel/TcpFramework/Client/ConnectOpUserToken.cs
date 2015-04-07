using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace TcpFramework.Client
{
    internal class ConnectOpUserToken
    {
        internal List<Message> ArrayOfMessageReadyToSend{get;set;}
        internal int ServerPort;
    }
}
