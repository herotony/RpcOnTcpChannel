using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TcpFramework.Common;

namespace TcpFramework.Client
{
    public class DeploySocketManager : BaseSocketManager
    {
        internal override ClientSetting GetLocalClientSetting()
        {
            return ReadConfigFile.GetClientSettingII(ReadConfigFile.ClientSettingType.DEPLOY);
        }
    }
}
