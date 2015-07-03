using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Net;

using TcpFramework.Common;


namespace TcpFramework.Client
{
    public class ClientSocketManager:BaseSocketManager
    {
        internal override ClientSetting GetLocalClientSetting()
        {
            return ReadConfigFile.GetClientSettingII(ReadConfigFile.ClientSettingType.HTTP);
        }
       
    }
}
