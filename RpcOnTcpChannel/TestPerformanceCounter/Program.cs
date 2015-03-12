using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

using TcpFramework.Common;

namespace TestPerformanceCounter
{
    class Program
    {
              
        static void Main(string[] args)
        {
            RegisterPerfOfTimePerIO();        

            Console.ReadKey();
        }

        private static void RegisterPerfOfTimePerIO() {

            SimplePerformanceCounter simplePerf = new SimplePerformanceCounter();

            CustomPerformanceCounter.PerformanceCounterManager pfm = new CustomPerformanceCounter.PerformanceCounterManager(simplePerf.CategoryName);

            Dictionary<string, PerformanceCounterType> dictPerfInfo = new Dictionary<string, PerformanceCounterType>();

            dictPerfInfo.Add(simplePerf.ClientConcurrentConnectionCounterName, PerformanceCounterType.NumberOfItems32);
            dictPerfInfo.Add(simplePerf.ClientHeartBeatConnectionCounterName, PerformanceCounterType.NumberOfItems32);
            dictPerfInfo.Add(simplePerf.ServerConcurrentConnectionCounterName, PerformanceCounterType.NumberOfItems32);
            dictPerfInfo.Add(simplePerf.ServerHeartBeatConnectionCounterName, PerformanceCounterType.NumberOfItems32);

            bool result = pfm.CreatePerformanceCounter(dictPerfInfo);

            if (result)
                Console.WriteLine("register success!");
            else
                Console.WriteLine("register failed!");            

        }
      
    }
}
