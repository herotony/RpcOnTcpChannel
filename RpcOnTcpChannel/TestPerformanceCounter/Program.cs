using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;



namespace TestPerformanceCounter
{
    class Program
    {
        private static CustomPerformanceCounter.PerformanceCounterManager pCounterManager = new CustomPerformanceCounter.PerformanceCounterManager("tcpframework_clientsocketprocessor");
        private static Random rand = new Random();
        
        static void Main(string[] args)
        {            
            Dictionary<string, string> dictPCounterInfo = new Dictionary<string, string>();

            dictPCounterInfo.Add("StartConnect", "StartConnect");
            dictPCounterInfo.Add("ProcessConnect", "ProcessConnect");
            dictPCounterInfo.Add("StartSend", "StartSend");
            dictPCounterInfo.Add("ProcessSend", "ProcessSend");
            dictPCounterInfo.Add("StartReceive", "StartReceive");
            dictPCounterInfo.Add("ProcessReceive", "ProcessReceive");
            dictPCounterInfo.Add("StartDisconnect", "StartDisconnect");
            dictPCounterInfo.Add("ProcessDisconnectAndCloseSocket", "ProcessDisconnectAndCloseSocket");
            dictPCounterInfo.Add("IO_Complete", "IO_Complete");

            bool result = pCounterManager.CreatePerformanceCounter(dictPCounterInfo);

            //pCounterManager.Increment("ProcessConngect");

            if (result)
                Console.WriteLine("finish ok!");
            else
                Console.WriteLine("finish fail!");

            

            //while (true) {

            //    RunIncrement();

            //    Thread.Sleep(1000);
            //    Console.WriteLine("t"+Convert.ToString(rand.Next(10000)));

            //    RunDecrement();

            //}

            //Console.WriteLine("again");

            Console.ReadKey();

        }

        private static void RunIncrement(){

            int t = rand.Next(1000);

            //for (int i = 0; i < t; i++)
            //    pCounterManager.Increment("StartSend");
            

        }

        private static void RunDecrement()
        {
            int t = rand.Next(100);

            //for (int i = 0; i < t; i++)
            //    pCounterManager.Decrement("StartSend");

        }
    }
}
