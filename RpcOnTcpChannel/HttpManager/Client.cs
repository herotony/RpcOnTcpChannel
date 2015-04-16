using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TcpFramework.Client;
using log4net;

namespace HttpManager
{
    public class Client
    {
        private static ILog logClient = LogManager.GetLogger(typeof(Client));

        public  enum route { common, log };

        public static string SendRequest(string command, string requestString,route routeType=route.common) {

            try {

                byte[] commandByte = Encoding.UTF8.GetBytes(command);
                int commandLength = commandByte.Length;
                byte[] commandLengthByte = BitConverter.GetBytes(commandLength);

                byte[] requestXmlByte = Encoding.UTF8.GetBytes(requestString);
                byte[] sendData = new byte[commandLengthByte.Length + commandByte.Length + requestXmlByte.Length];

                Buffer.BlockCopy(commandLengthByte, 0, sendData, 0, commandLengthByte.Length);
                Buffer.BlockCopy(commandByte, 0, sendData, commandLengthByte.Length, commandByte.Length);
                Buffer.BlockCopy(requestXmlByte, 0, sendData, commandLengthByte.Length + commandByte.Length, requestXmlByte.Length);

                if (routeType == route.common)
                {
                    ClientSocketManager csmgr = new ClientSocketManager();

                    string message = string.Empty;

                    byte[] retData = csmgr.SendRequest(sendData, ref message);

                    if (message.Equals("socket:ok"))
                    {
                        if (retData != null && retData.Length > 8)
                            return Encoding.UTF8.GetString(retData, 8, retData.Length - 8);
                        else
                            return "socket:failbynull";
                    }
                    else {

                        return message;
                    }
                }
                else if (routeType == route.log)
                {

                    LogClientSocketManager csmgr = new LogClientSocketManager();

                    string message = string.Empty;

                    byte[] retData = csmgr.SendRequest(sendData, ref message);

                    if (message.Equals("socket:ok"))
                    {
                        if (retData != null && retData.Length > 8)
                            return Encoding.UTF8.GetString(retData, 8, retData.Length - 8);
                        else
                            return "socket:failbynull";
                    }
                    else
                        return message;
                }
                else
                    return "failbyunknown";
            }
            catch(Exception clientErr) {

                logClient.Error(string.Format("SendRequest异常!\r\ncmd:{0}\r\nrequest:{1}\r\nerr:{2}\r\nstackTrace:{3}\r\n", command, requestString, clientErr.Message, clientErr.StackTrace), clientErr);
            }            

            return "fail";
        }                
    }
}
