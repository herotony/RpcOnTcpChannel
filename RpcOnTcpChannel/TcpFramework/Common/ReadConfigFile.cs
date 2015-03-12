using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;

using TcpFramework.Client;
using TcpFramework.Server;


namespace TcpFramework.Common
{
    internal class ReadConfigFile
    {
        private static string path = string.Empty;
        private static string fileName = "socketsetting.txt";

        private static int opsToPreAllocate = 2;
        private static int receivePrefixLength = 4;
        private static int sendPrefixLength = 4;

        private static IPEndPoint IPInfo = null;
        private static int maxConnectSocketCount = 0;
        private static int maxDataSocketCount = 0;
        private static int bufferSize = 0;
        private static int timeOutByMS = 0;
        private static bool useKeepAlive = true;
        private static int numberSendCountPerConnection = 1;        

        static ReadConfigFile() {            
                
            path = AppDomain.CurrentDomain.BaseDirectory;
            if (!path.EndsWith(@"\"))
                path += @"\";
        }

        private static Dictionary<string, string> GetKeyValueSetting()
        {
            Dictionary<string, string> dictKeyValueSetting = new Dictionary<string, string>();

            string content = string.Empty;

            using (FileStream fs = new FileStream(path + fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                StreamReader reader = new StreamReader(fs);
                content = reader.ReadToEnd();
            }

            if (string.IsNullOrEmpty(content))
                return null;

            string[] lines = content.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines == null || lines.Length.Equals(0))
                return null;

            for (int i = 0; i < lines.Length; i++)
            {
                string[] configInfo = lines[i].Split(':');

                if (configInfo.Length < 2)
                    continue;

                if (dictKeyValueSetting.ContainsKey(configInfo[0]))
                    dictKeyValueSetting[configInfo[0]] = configInfo[1];
                else
                    dictKeyValueSetting.Add(configInfo[0], configInfo[1]);
            }

            return dictKeyValueSetting.Count > 0 ? dictKeyValueSetting : null;
        }

        internal static ClientSetting GetClientSetting() {

            int shouldBingoCount = 6;
            int actualBingoCount = 0;

            Dictionary<string, string> dictOriginalSetting = GetKeyValueSetting();

            foreach (string key in dictOriginalSetting.Keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                string lowerKey = key.ToLower().Trim();
                string value = dictOriginalSetting[key];

                if (lowerKey.StartsWith("hostinfo"))
                {
                    IPInfo = ParseHost(value);
                    if (IPInfo == null)
                        return null;

                    actualBingoCount++;

                }
                else if (lowerKey.StartsWith("connectsocket_count"))
                {
                    if (!int.TryParse(value, out maxConnectSocketCount))
                        return null;

                    actualBingoCount++;

                }
                else if (lowerKey.StartsWith("datasocket_count"))
                {
                    if (!int.TryParse(value, out maxDataSocketCount))
                        return null;

                    actualBingoCount++;
                }
                else if (lowerKey.StartsWith("buffersize"))
                {
                    if (!int.TryParse(value, out bufferSize))
                        return null;

                    actualBingoCount++;
                }
                else if (lowerKey.StartsWith("timeout"))
                {
                    if (!int.TryParse(value, out timeOutByMS))
                        return null;

                    actualBingoCount++;
                }
                else if (lowerKey.StartsWith("keepalive")) {

                    if (!bool.TryParse(value, out useKeepAlive))
                        return null;

                    actualBingoCount++;
                }               

            }

            if (actualBingoCount.Equals(shouldBingoCount))
            {
                return new ClientSetting(IPInfo, numberSendCountPerConnection, maxConnectSocketCount,
                    maxDataSocketCount, bufferSize, receivePrefixLength, sendPrefixLength, opsToPreAllocate, timeOutByMS);
            }
            else
                throw new ArgumentNullException("ClientSettings");	
        }

        internal static ServerSetting GetServerSetting()
        {
            int shouldBingoCount = 5;
            int actualBingoCount = 0;

            Dictionary<string, string> dictOriginalSetting = GetKeyValueSetting();

            foreach (string key in dictOriginalSetting.Keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                string lowerKey = key.ToLower().Trim();
                string value = dictOriginalSetting[key];

                if (lowerKey.StartsWith("hostinfo"))
                {
                    IPInfo = ParseHost(value);
                    if (IPInfo == null)
                        return null;

                    actualBingoCount++;

                }
                else if (lowerKey.StartsWith("connectsocket_count"))
                {
                    if (!int.TryParse(value, out maxConnectSocketCount))
                        return null;

                    actualBingoCount++;

                }
                else if (lowerKey.StartsWith("datasocket_count"))
                {
                    if (!int.TryParse(value, out maxDataSocketCount))
                        return null;

                    actualBingoCount++;
                }
                else if (lowerKey.StartsWith("buffersize"))
                {
                    if (!int.TryParse(value, out bufferSize))
                        return null;

                    actualBingoCount++;
                }
                else if (lowerKey.StartsWith("keepalive")) {

                    if (!bool.TryParse(value, out useKeepAlive))
                        return null;

                    actualBingoCount++;
                }              
            }

            if (actualBingoCount.Equals(shouldBingoCount))
            {
                return new ServerSetting(IPInfo, maxConnectSocketCount,maxDataSocketCount, bufferSize);
            }
            else
                throw new ArgumentNullException("ServerSettings");
        }

        private static IPEndPoint ParseHost(string value)
        {
            string[] arr = value.Split('|');

            if (value.Length < 2)
                return null;

            IPAddress ipAddr;

            if (!arr[0].ToLower().StartsWith("any"))
            {

                if (!IPAddress.TryParse(arr[0], out ipAddr))
                    return null;
            }
            else
                ipAddr = IPAddress.Any;

            int port;

            if (!int.TryParse(arr[1], out port))
                return null;

            return new IPEndPoint(ipAddr, port);
        }

    }
}
