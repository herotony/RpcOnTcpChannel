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

        public static string SendRequest(string command, string requestString) {

            try {

                Queue<ClientData> queue = new Queue<ClientData>();
                int totalLength = 0;

                totalLength = DataManager.PushClientDataToQueueAndFeedbackLength(queue, "3");
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "sendrequest_1");
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, command);
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, requestString);

                byte[] sendData = DataManager.GetSendData(queue, totalLength);

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
                else
                {
                    logClient.ErrorFormat("cmd:{0} fail in status:{1} request:{2} ", command, message, requestString);
                    return message;
                }                   
               
            }
            catch(Exception clientErr) {

                logClient.Error(string.Format("SendRequest_1异常!\r\ncmd:{0}\r\nrequest:{1}\r\nerr:{2}\r\nstackTrace:{3}\r\n", command, requestString, clientErr.Message, clientErr.StackTrace), clientErr);
            }            

            return "fail";
        }

        public static string SendRequest(string command, string requestString,string ipAddress, string userAgent, bool isNeedEncrypt) {

            try
            {
                Queue<ClientData> queue = new Queue<ClientData>();
                int totalLength = 0;

                totalLength = DataManager.PushClientDataToQueueAndFeedbackLength(queue, "6");
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "sendrequest_2");
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, command);
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, ipAddress);
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, userAgent);
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, isNeedEncrypt?"true":"false");
                totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, requestString);

                byte[] sendData = DataManager.GetSendData(queue, totalLength);

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
                else
                {
                    logClient.ErrorFormat("cmd:{0} fail in status:{1} request:{2} [from ip:{3},ua:{4},encrypt:{5}]", command, message, requestString,ipAddress,userAgent,isNeedEncrypt);
                    return message;
                }                         
            }
            catch (Exception clientErr)
            {
                logClient.Error(string.Format("SendRequest_2异常!\r\ncmd:{0}\r\nrequest:{1}\r\nerr:{2}\r\nstackTrace:{3}\r\n", command, requestString, clientErr.Message, clientErr.StackTrace), clientErr);
            }

            return "fail";
        }

    }
}
