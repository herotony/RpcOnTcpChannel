using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using TcpFramework.Client;

namespace TestTcpFramework
{
    class Program
    {
        private static ILog log = LogManager.GetLogger("client");

        static void Main(string[] args)
        {
            string message = string.Empty;
            byte[]  result = ClientSocketManager.SendRequest(Encoding.UTF8.GetBytes("秦琼战关公!"), ref message);

            if (result == null)
                log.Error(message);
            else
                log.Info(Encoding.UTF8.GetString(result,8,result.Length-8));

            Console.ReadKey();

        }
    }
}
