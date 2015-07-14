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

        public static string GetRequestString(byte[] inputData,ref string command,ref string ipAddress,ref string userAgent,ref bool isNeedEncrypt)
        {
            try {

                string[] arrInputParam = DataManager.DecodeByteData(inputData);

                command = arrInputParam[1];
                ipAddress = arrInputParam[2];
                userAgent = arrInputParam[3];
                isNeedEncrypt = arrInputParam[4].Equals("true");

                return arrInputParam[5];
            }
            catch (Exception serverErr) {

                logServer.Error(string.Format("分析请求指令失败!\r\nerr:{0}\r\nstackTrace:{1}",serverErr.Message,serverErr.StackTrace), serverErr);
            }

            return null;
        }
    }
}
