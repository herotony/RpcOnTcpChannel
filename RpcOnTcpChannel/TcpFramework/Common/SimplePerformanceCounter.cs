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

        public SimplePerformanceCounter(bool isCreateCounter = false,bool isServer=false) {

            this.CategoryName = "__TcpFrameworkPerfCounter__";
            if (!isServer)
            {
                this.ClientConcurrentConnectionCounterName = "Client Concurrent Connection Count";
                this.ClientResuseConnectionCounterName = "Client Reuse Connection Count";
                this.ClientIdleConnectionCounterName = "Client Idle Connection Count";
            }
            else
                this.ServerConcurrentConnectionCounterName = "Server Concurrent Connection Count";           

            if (isCreateCounter) {

                string processName = Process.GetCurrentProcess().ProcessName;

                if (!isServer)
                {
                    this.PerfConcurrentClientConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientConcurrentConnectionCounterName, "client_concurrent_connection_" + processName, false);
                    this.PerfClientReuseConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientResuseConnectionCounterName, "client_reuse_connection_" + processName, false);
                    this.PerfClientIdleConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientIdleConnectionCounterName, "client_idle_connection_" + processName, false);
                }
                else
                    this.PerfConcurrentServerConnectionCounter = new PerformanceCounter(this.CategoryName, this.ServerConcurrentConnectionCounterName, "server_concurrent_connection_" + processName, false);              
            }            
        }
    }
}
