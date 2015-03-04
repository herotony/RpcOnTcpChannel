using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpRpc;
using SharpRpc.Settings;
using SharpRpc.Topology;

namespace TestSharpRpcServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var topologyLoader = new TopologyLoader("../../../Topology/topology.txt", Encoding.UTF8, new TopologyParser());
            var settingsLoader = new SettingsLoader("../../../Settings/Host.txt",Encoding.UTF8, new HostSettingsParser());
            var kernel = new RpcClientServer(topologyLoader, new TimeoutSettings(5000), settingsLoader);

            kernel.StartHost();

            string line = Console.ReadLine();
            while (line != "exit")
            {
                line = Console.ReadLine();
            }

            kernel.StopHost();

        }
    }
}
