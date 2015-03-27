using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using log4net;
using TcpFramework.Server;

namespace TestServerForTcpFramework
{
    class Program
    {
        private static ILog log = LogManager.GetLogger("server");
        private static ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private static Thread thPeek;
        private static ManualResetEvent mResetEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            SocketListener listener = new SocketListener(ParseData);

            thPeek = new Thread(RunPeek);
            thPeek.IsBackground = true;
            thPeek.Start();

            Console.ReadKey();
        }

        private static byte[] ParseData(byte[] input) {

            string inputdata = Encoding.UTF8.GetString(input);

            //log.Info(inputdata);
            

            string result = string.Format("date:{0} - {1}", DateTime.Now, inputdata);

            logQueue.Enqueue(result);

            return Encoding.UTF8.GetBytes(result);
        }

        private static void RunPeek() {

            WaitHandle[] waits = new WaitHandle[] { mResetEvent };

            while (true) {

                string logStr = string.Empty;

                while (logQueue.TryDequeue(out logStr)) {

                    log.InfoFormat(logStr);
                }

                if (WaitHandle.WaitAny(waits, 100) != WaitHandle.WaitTimeout)
                    break;
            }

        }
    }
}
