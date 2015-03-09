using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace CustomPerformanceCounter
{
    public class PerformanceCounterManager
    {
        private string categoryName;        

        private CounterCreationDataCollection counterCollection = new CounterCreationDataCollection();        

        public PerformanceCounterManager(string categoryName) {

            this.categoryName = categoryName;
            if (PerformanceCounterCategory.Exists(categoryName))
            {
                PerformanceCounterCategory.Delete(categoryName);
            }
        }

        //key:计数器名称,value:计数器描述
        public bool CreatePerformanceCounter(Dictionary<string, string> dictCounterInfo) {

            try {
                foreach (string counterName in dictCounterInfo.Keys)
                {
                    AddSimplePerformanceCounter(counterName, dictCounterInfo[counterName]);
                }

                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.MultiInstance, counterCollection);

                return true;
            }
            catch (Exception e) {

                Console.Write(e.Message);
            }

            return false;           
        }       

        private void AddSimplePerformanceCounter(string counterName,string counterDescription) {

            CounterCreationData data = new CounterCreationData(counterName, counterDescription, PerformanceCounterType.NumberOfItems64);

            counterCollection.Add(data);                        
        }
    }
}
