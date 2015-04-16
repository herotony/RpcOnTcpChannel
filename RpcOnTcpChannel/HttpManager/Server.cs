using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;

namespace HttpManager
{
    public class Server
    {
        private static ILog logServer = LogManager.GetLogger(typeof(Server));
        public static string GetRequestString(byte[] inputData, ref string command) {

            try {

                int commandLength = BitConverter.ToInt32(inputData, 0);
                command = Encoding.UTF8.GetString(inputData, 4, commandLength);

                return Encoding.UTF8.GetString(inputData, 4 + commandLength, inputData.Length - 4 - commandLength);
            }
            catch (Exception serverErr) {

                logServer.Error(string.Format("分析请求指令失败!\r\nerr:{0}\r\nstackTrace:{1}",serverErr.Message,serverErr.StackTrace), serverErr);
            }

            return null;
        }
    }
}
