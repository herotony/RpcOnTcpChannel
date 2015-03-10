using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;



namespace TestPerformanceCounter
{
    class Program
    {
        private static CustomPerformanceCounter.PerformanceCounterManager pfm = new CustomPerformanceCounter.PerformanceCounterManager("tcpframework_clientsocketprocessor");
      
        static void Main(string[] args)
        {
            RegisterPerfOfTimePerIO();        

            Console.ReadKey();
        }

        private static void RegisterPerfOfTimePerIO() {

            Dictionary<string, PerformanceCounterType> dictPerfInfo = new Dictionary<string, PerformanceCounterType>();

            dictPerfInfo.Add("Consume Second/Per IO_Complete", PerformanceCounterType.AverageTimer32);
            dictPerfInfo.Add("Total IO_Complete", PerformanceCounterType.AverageBase);

            bool result = pfm.CreatePerformanceCounter(dictPerfInfo);

            if (result)
                Console.WriteLine("register success!");
            else
                Console.WriteLine("register failed!");            

        }
      
    }
}
