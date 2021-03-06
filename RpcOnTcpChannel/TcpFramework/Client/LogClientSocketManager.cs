﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using TcpFramework.Common;

namespace TcpFramework.Client
{
    public class LogClientSocketManager :BaseSocketManager
    {
        internal override ClientSetting GetLocalClientSetting()
        {
            return ReadConfigFile.GetClientSettingII(ReadConfigFile.ClientSettingType.LOG);
        }
    }
}
