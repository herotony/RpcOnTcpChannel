using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.IO;

using SharpRpc;
using SharpRpc.Topology;

namespace TestSharpRpcClient
{
    class Program
    {
        private static TopologyLoader toplogyLoader;
        private static RpcClient rpcClient;
        private static IProcessGoodDetail.IGoodManager instance;

        private static int successCount = 0;
        private static int failCount = 0;

        static void Main(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            //string content = RunTestHttpData();

            toplogyLoader = new TopologyLoader("../../../Topology/topology.txt", Encoding.UTF8, new TopologyParser());
            rpcClient = new RpcClient(toplogyLoader, new TimeoutSettings(500));

            instance = rpcClient.GetService<IProcessGoodDetail.IGoodManager>();

            int testCount = 10000;
            Task[] tsks = new Task[testCount];

            for (int i = 0; i < tsks.Length; i++)
            {

                tsks[i] = Task.Factory.StartNew(() => { RunTestData(); });
                //tsks[i] = Task.Factory.StartNew(() => { RunTestHttpData(); });

            }

            Task.WaitAll(tsks);

            //for (int i = 0; i < testCount; i++)
            //{

            //    RunTestData();
            //}

            sw.Stop();
            
            Console.WriteLine(string.Format("耗时:{0} ms success:{1}  fail:{2}", sw.ElapsedMilliseconds,successCount,failCount));
            Console.ReadKey();
        }

        private static void RunTestData() {


            try {
                string[] ids = new string[] { "12", "16" };
                string result = instance.GetGoodDetail(ids, 1, 2, "北京");

                if (!string.IsNullOrEmpty(result))
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);

                //Console.WriteLine(string.Format("tid:{0} {1}", Thread.CurrentThread.ManagedThreadId, result));
            }
            catch {

                Interlocked.Increment(ref failCount);
            }                       
        }

        private static string  RunTestHttpData() {

            try {
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create("http://localhost:14322/default.aspx");

                byte[] postdata = new byte[1024];

                httpRequest.Method = "POST";
                httpRequest.ContentLength = postdata.Length;
                Stream reqStream = httpRequest.GetRequestStream();
                reqStream.Write(postdata, 0, postdata.Length);
                reqStream.Close();

                HttpWebResponse response = (HttpWebResponse)httpRequest.GetResponse();

                StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                string responseContent = sr.ReadToEnd();

                if (!string.IsNullOrEmpty(responseContent))
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);

                //Console.WriteLine(responseContent);

                return responseContent;
            }
            catch {

                Interlocked.Increment(ref failCount);
            }

            return string.Empty;
        }

      
    }
}
