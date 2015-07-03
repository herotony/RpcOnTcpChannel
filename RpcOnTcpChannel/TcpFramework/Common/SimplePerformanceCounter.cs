using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TcpFramework.Common
{
    public class SimplePerformanceCounter
    {
        public string CategoryName{get;private set;}

        public string ClientConcurrentConnectionCounterName { get; private set; }
        public string ServerConcurrentConnectionCounterName { get; private set; }        

        public PerformanceCounter PerfConcurrentClientConnectionCounter { get; private set; }
        public PerformanceCounter PerfConcurrentServerConnectionCounter { get; private set; }

        public string ClientResuseConnectionCounterName { get; private set; }
        public PerformanceCounter PerfClientReuseConnectionCounter { get; private set; }

        public string ClientIdleConnectionCounterName { get; private set; }
        public PerformanceCounter PerfClientIdleConnectionCounter { get; private set; }

        public string ClientRequestSuccessCounterName { get; private set; }
        public PerformanceCounter PerfClientRequestSuccessCounter { get; private set; }

        public string ClientRequestFailCounterName { get; private set; }
        public PerformanceCounter PerfClientRequestFailCounter { get; private set; }

        public string ClientRequestTotalCounterName { get; private set; }
        public PerformanceCounter PerfClientRequestTotalCounter { get; private set; }

        public SimplePerformanceCounter(bool isCreateCounter = false,bool isServer=false,string specialName="") {

            this.CategoryName = "__TcpFrameworkPerfCounter__";
            if (!isServer)
            {
                this.ClientConcurrentConnectionCounterName = "Client Concurrent Connection Count";
                this.ClientResuseConnectionCounterName = "Client Reuse Connection Count";
                this.ClientIdleConnectionCounterName = "Client Idle Connection Count";
                this.ClientRequestSuccessCounterName = "Client Request Success Count/PerSecond";
                this.ClientRequestFailCounterName = "Client Request Fail Count/PerSecond";
                this.ClientRequestTotalCounterName = "Client Request Total Count/PerSecond";
            }
            else
                this.ServerConcurrentConnectionCounterName = "Server Concurrent Connection Count";           

            if (isCreateCounter) {

                string processName = Process.GetCurrentProcess().ProcessName;
                string processId = Process.GetCurrentProcess().Id.ToString();

                if (!isServer)
                {
                    this.PerfConcurrentClientConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientConcurrentConnectionCounterName, "client_concurrent_connection_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);
                    this.PerfClientReuseConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientResuseConnectionCounterName, "client_reuse_connection_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);
                    this.PerfClientIdleConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientIdleConnectionCounterName, "client_idle_connection_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);
                    this.PerfClientRequestSuccessCounter = new PerformanceCounter(this.CategoryName, this.ClientRequestSuccessCounterName, "client_request_success_persecond_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);
                    this.PerfClientRequestFailCounter = new PerformanceCounter(this.CategoryName, this.ClientRequestFailCounterName, "client_request_fail_persecond_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);
                    this.PerfClientRequestTotalCounter = new PerformanceCounter(this.CategoryName, this.ClientRequestTotalCounterName, "client_request_total_persecond_" + processName + "(" + processId + ")" + AppendSuffixName(specialName), false);                    
                }
                else
                {                    
                    this.PerfConcurrentServerConnectionCounter = new PerformanceCounter(this.CategoryName, this.ServerConcurrentConnectionCounterName, "server_concurrent_connection_" + processName+"("+processId+")", false);
                }
            }            
        }

        private string AppendSuffixName(string name) {

            if (string.IsNullOrEmpty(name))
                return "";
            else
                return "(" + name + ")";
        }
    }
}
