using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

using log4net;
using TcpFramework.Client;

namespace TestTcpFramework
{
    class Program
    {
        private static ILog log = LogManager.GetLogger("client");

        static void Main(string[] args)
        {
            int totalLoopcount = 0;        

            while (true) {

                string message = string.Empty;

                Console.WriteLine("start");

                int testCount = 1000;
                int faileCount = 0;
                int successCount = 0;

                Task[] tasks = new Task[testCount];
                Stopwatch sw = new Stopwatch();
                sw.Start();

                Random rand = new Random();

                Random rnd = new Random();

                for (int i = 0; i < testCount; i++)
                {

                    tasks[i] = Task.Factory.StartNew(() =>
                    {

                        Stopwatch _sw = new Stopwatch();
                        _sw.Start();
                        //byte[] result = ClientSocketManager.SendRequest(Encoding.UTF8.GetBytes(string.Format("秦琼战关公! on id:{0}", rnd.Next(10000))), ref message);
                        ClientSocketManager smgr = new ClientSocketManager();
                        byte[] result = smgr.SendRequest(Encoding.UTF8.GetBytes(string.Format("秦琼战关公! on id:{0}", rnd.Next(10000))), ref message);

                        if (result == null)
                        {
                            Interlocked.Increment(ref faileCount);
                            //log.Error(message);
                        }
                        else
                        {
                            Interlocked.Increment(ref successCount);
                            int tranId = BitConverter.ToInt32(result, 0);
                            int sid = BitConverter.ToInt32(result, 4);
                            string feedback = Encoding.UTF8.GetString(result, 8, result.Length - 8);

                            _sw.Stop();

                            //if (_sw.ElapsedMilliseconds > 6)
                            //    log.Error("long time:" + _sw.ElapsedMilliseconds.ToString());
                            //log.Info(message);
                            //log.Info(string.Format("tid:{0} in sid:{1} with cnt:{2} consumetime:{3} ms", tranId, sid, feedback, _sw.ElapsedMilliseconds));
                        }

                        //int k = rand.Next(100000);                  

                        //log.Info(string.Format("{0} start", k));

                        //Interlocked.Increment(ref successCount);

                        //byte[] info = Encoding.UTF8.GetBytes(string.Format("测试 on {0} index", k));

                        //byte[] b = new byte[info.Length];

                        //Buffer.BlockCopy(info, 0, b, 0, info.Length);

                        //Thread.Sleep(100);

                        //log.Info(string.Format("{0} over", k));

                    });
                   

                }

                //Thread.Sleep(10000);

                Console.WriteLine("all queue over!");

                Task.WaitAll(tasks);

                sw.Stop();

                totalLoopcount++;

                log.Info(string.Format("共耗时:{0}毫秒,ok:{1},fail:{2} loop:{3}", sw.ElapsedMilliseconds, successCount, faileCount,totalLoopcount));

                Thread.Sleep(100);

            }                       

            Console.ReadKey();

        }
    }
}
