using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;

using SharpRpc;
using SharpRpc.Topology;

namespace SimpleHttpTestClient
{
    /// <summary>
    /// defaultHandle 的摘要说明
    /// </summary>
    public class defaultHandle : IHttpHandler
    {
        private static object lockObject= new object();
        private static TopologyLoader toplogyLoader;
        private static RpcClient rpcClient;
        private static IProcessGoodDetail.IGoodManager instance;
        private static Random randNumber = new Random();

        public void ProcessRequest(HttpContext context)
        {
            if (toplogyLoader == null)
            {
                lock (lockObject) {

                    if (toplogyLoader == null)
                    {
                        string path = context.Server.MapPath(context.Request.Url.AbsolutePath);

                        if (path.EndsWith("defaultHandle.ashx"))
                            path = path.Substring(0, path.Length - 18);

                        toplogyLoader = new TopologyLoader(path+"topology.txt", Encoding.UTF8, new TopologyParser());
                        rpcClient = new RpcClient(toplogyLoader, new TimeoutSettings(500));
                        instance = rpcClient.GetService<IProcessGoodDetail.IGoodManager>();
                    }
                }                
            }            

            string[] ids = new string[] { randNumber.Next(1000).ToString(), randNumber.Next(10000).ToString() };
            string result = instance.GetGoodDetail(ids, randNumber.Next(100), randNumber.Next(1000), "北京");

            context.Response.ContentType = "text/plain";
            context.Response.Write(result);
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}