using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TcpFramework.Client;

namespace HttpManager
{
    public class Client
    {
        public static string SendRequest(string command, string requestXml) {

            byte[] commandByte = Encoding.UTF8.GetBytes(command);
            int commandLength = commandByte.Length;
            byte[] commandLengthByte = BitConverter.GetBytes(commandLength);

            byte[] requestXmlByte = Encoding.UTF8.GetBytes(requestXml);
            byte[] sendData = new byte[commandLengthByte.Length + commandByte.Length + requestXmlByte.Length];

            Buffer.BlockCopy(commandLengthByte, 0, sendData, 0, commandLengthByte.Length);
            Buffer.BlockCopy(commandByte, 0, sendData, commandLengthByte.Length, commandByte.Length);
            Buffer.BlockCopy(requestXmlByte, 0, sendData, commandLengthByte.Length + commandByte.Length, requestXmlByte.Length);

            ClientSocketManager csmgr = new ClientSocketManager();

            string message = string.Empty;

            byte[] retData = csmgr.SendRequest(sendData, ref message);

            if (message.Equals("ok")) {

                return Encoding.UTF8.GetString(retData, 8, retData.Length - 8);
            }

            return "fail";
        }                
    }
}
