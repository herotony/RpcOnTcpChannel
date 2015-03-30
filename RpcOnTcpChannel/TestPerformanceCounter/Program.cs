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
            if (args == null || args.Length.Equals(0))
                throw new ArgumentException("请务必指定注册计数器所在终端类型参数 [example:\"server\" or \"client\" or \"all\"]");
           

            RegisterPerfOfTimePerIO(args[0].ToLower());        

            Console.ReadKey();
        }

        private static void RegisterPerfOfTimePerIO(string terminalType) {

            SimplePerformanceCounter simplePerf = null;

            if (terminalType.Equals("client"))
                simplePerf = new SimplePerformanceCounter(false, false);
            else if (terminalType.Equals("server"))
                simplePerf = new SimplePerformanceCounter(false, true);
            else
                simplePerf = new SimplePerformanceCounter(false, false);


            CustomPerformanceCounter.PerformanceCounterManager pfm = new CustomPerformanceCounter.PerformanceCounterManager(simplePerf.CategoryName);

            Dictionary<string, PerformanceCounterType> dictPerfInfo = new Dictionary<string, PerformanceCounterType>();

            if (terminalType.Equals("client") || terminalType.Equals("all"))
            {
                dictPerfInfo.Add(simplePerf.ClientConcurrentConnectionCounterName, PerformanceCounterType.NumberOfItems32);
                dictPerfInfo.Add(simplePerf.ClientResuseConnectionCounterName, PerformanceCounterType.NumberOfItems32);
                dictPerfInfo.Add(simplePerf.ClientIdleConnectionCounterName, PerformanceCounterType.NumberOfItems32);
            }
            
            if (terminalType.Equals("server") || terminalType.Equals("all"))
            {
                if (terminalType.Equals("all"))
                    simplePerf = new SimplePerformanceCounter(false, true);

                dictPerfInfo.Add(simplePerf.ServerConcurrentConnectionCounterName, PerformanceCounterType.NumberOfItems32);
            }
                                              
            bool result = pfm.CreatePerformanceCounter(dictPerfInfo);

            if (result)
                Console.WriteLine("register success!");
            else
                Console.WriteLine("register failed!");            

        }
      
    }
}
