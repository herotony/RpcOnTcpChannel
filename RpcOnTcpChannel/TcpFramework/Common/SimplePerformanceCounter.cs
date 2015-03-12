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
        public string ClientHeartBeatConnectionCounterName { get; private set; }
        public string ServerHeartBeatConnectionCounterName { get; private set; }

        public PerformanceCounter PerfConcurrentClientConnectionCounter { get; private set; }
        public PerformanceCounter PerfConcurrentServerConnectionCounter { get; private set; }
        public PerformanceCounter PerfHeartBeatStatusClientConnectionCounter { get; private set; }
        public PerformanceCounter PerfHeartBeatStatusServerConnectionCounter { get; private set; }

        public SimplePerformanceCounter(bool isCreateCounter = false) {

            this.CategoryName = "__TcpFrameworkPerfCounter__";
            this.ClientConcurrentConnectionCounterName = "Client Concurrent Connection Count";
            this.ServerConcurrentConnectionCounterName = "Server Concurrent Connection Count";
            this.ClientHeartBeatConnectionCounterName = "Client HearBeat Connection Count";
            this.ServerHeartBeatConnectionCounterName = "Server HearBeat Connection Count";

            if (isCreateCounter) {

                string processName = Process.GetCurrentProcess().ProcessName;

                this.PerfConcurrentClientConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientConcurrentConnectionCounterName,"client_concurrent_connection_"+processName, false);
                this.PerfConcurrentServerConnectionCounter = new PerformanceCounter(this.CategoryName, this.ServerConcurrentConnectionCounterName, "server_concurrent_connection_" + processName, false);
                this.PerfHeartBeatStatusClientConnectionCounter = new PerformanceCounter(this.CategoryName, this.ClientHeartBeatConnectionCounterName, "client_heartbeat_connection_" + processName, false);
                this.PerfHeartBeatStatusServerConnectionCounter = new PerformanceCounter(this.CategoryName, this.ServerHeartBeatConnectionCounterName, "server_heartbeat_connection_" + processName, false);
            }            
        }
    }
}
