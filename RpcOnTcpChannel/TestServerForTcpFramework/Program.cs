using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using TcpFramework.Server;

namespace TestServerForTcpFramework
{
    class Program
    {
        private static ILog log = LogManager.GetLogger("server");

        static void Main(string[] args)
        {
            SocketListener listener = new SocketListener(ParseData);

            Console.ReadKey();
        }

        private static byte[] ParseData(byte[] input) {

            string inputdata = Encoding.UTF8.GetString(input);

            log.Info(inputdata);

            string result = string.Format("date:{0} - {1}", DateTime.Now, inputdata);


            return Encoding.UTF8.GetBytes(result);
        }
    }
}
