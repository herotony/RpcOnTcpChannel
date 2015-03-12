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
        private static Thread thbyside;

        static void Main(string[] args)
        {
            thbyside = new Thread(Run);
            thbyside.IsBackground = true;
            thbyside.Start();            

            //string content = RunTestHttpData();


            toplogyLoader = new TopologyLoader("../../../Topology/topology.txt", Encoding.UTF8, new TopologyParser());
            rpcClient = new RpcClient(toplogyLoader, new TimeoutSettings(500));

            while (true) {

                Stopwatch sw = new Stopwatch();
                sw.Start();

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



                Console.WriteLine(string.Format("耗时:{0} ms success:{1}  fail:{2}", sw.ElapsedMilliseconds, successCount, failCount));

                ConsoleKeyInfo keyInfo = Console.ReadKey();

                if (keyInfo.Key == ConsoleKey.Enter)
                    break;

                Thread.Sleep(1000);
            }

            
            Console.ReadKey();
        }

        private static void Run() {

            while (true) {

                Console.WriteLine("finished:{0},sucess:{1},fail:{2}", successCount + failCount, successCount, failCount);

                Thread.Sleep(6000);
            }
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
