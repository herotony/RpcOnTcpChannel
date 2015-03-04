using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using SharpRpc;
using SharpRpc.Topology;

namespace TestSharpRpcClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            int testCount = 10000;
            //Task[] tsks = new Task[testCount];

            //for (int i = 0; i < tsks.Length; i++) {

            //    tsks[i] = Task.Factory.StartNew(() => { RunTestData(); });

            //}

            //Task.WaitAll(tsks);

            for (int i = 0; i < testCount; i++) {

                RunTestData();
            }

            sw.Stop();

            Console.WriteLine(string.Format("耗时:{0} ms", sw.ElapsedMilliseconds));
            Console.ReadKey();
        }

        private static void RunTestData() {

            var topologyLoader = new TopologyLoader("../../../Topology/topology.txt", Encoding.UTF8, new TopologyParser());
            var client = new RpcClient(topologyLoader, new TimeoutSettings(500));

            var interfaceFunction = client.GetService<IProcessGoodDetail.IGoodManager>();

            string[] ids = new string[] { "12", "16" };

            string result = interfaceFunction.GetGoodDetail(ids, 1, 2, "北京");

            Console.WriteLine(result);
        }
    }
}
