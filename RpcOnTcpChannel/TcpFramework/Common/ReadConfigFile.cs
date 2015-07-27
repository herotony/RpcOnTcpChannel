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

        private static Dictionary<string, string> GetKeyValueSetting(string settingFileName="")
        {
            Dictionary<string, string> dictKeyValueSetting = new Dictionary<string, string>();

            string content = string.Empty;

            using (FileStream fs = new FileStream(path + (settingFileName==""?fileName:settingFileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

        internal static ClientSetting GetClientSetting(string settingFileName="") {

            int shouldBingoCount = 6;
            int actualBingoCount = 0;

            Dictionary<string, string> dictOriginalSetting = GetKeyValueSetting(settingFileName);
            IPEndPoint[] ipEPs = null;

            foreach (string key in dictOriginalSetting.Keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                string lowerKey = key.ToLower().Trim();
                string value = dictOriginalSetting[key];

                if (lowerKey.StartsWith("hostinfo"))
                {
                    string[] ipSetting = value.Split(',');
                    ipEPs = new IPEndPoint[ipSetting.Length];

                    for (int i = 0; i < ipEPs.Length; i++) {

                        IPEndPoint _IPInfo = ParseHost(ipSetting[i]);
                        if (_IPInfo == null)
                            return null;

                        ipEPs[i] = _IPInfo;
                    }
                   
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
                return new ClientSetting(ipEPs, numberSendCountPerConnection, maxConnectSocketCount,
                    maxDataSocketCount, bufferSize, receivePrefixLength, sendPrefixLength, opsToPreAllocate, timeOutByMS);
            }
            else
                throw new ArgumentNullException("ClientSettings");	
        }

        internal enum ClientSettingType { HTTP, LOG, DEPLOY };

        internal static ClientSetting GetClientSettingII(ClientSettingType clientSettingType) {

            int shouldBingoCount = 6;
            int actualBingoCount = 0;

            Dictionary<string, string> dictOriginalSetting = GetKeyValueSettingII();

            if (dictOriginalSetting == null)
            {
                LogManager.Log("未能正确读取socketsetting.txt中的配置信息");
                return null;
            }

            IPEndPoint[] ipEPs = null;

            string keyHeadInfo = clientSettingType.ToString().ToLower()+"-";
            var selectDictKey = from keyInfo in dictOriginalSetting where keyInfo.Key.StartsWith(keyHeadInfo) select keyInfo;

            if (selectDictKey.Count().Equals(0))
                throw new ArgumentNullException(keyHeadInfo + "ClientSettings");
                       
            try {

                foreach (string key in selectDictKey.ToDictionary(p => p.Key, p => p.Value).Keys)
                {
                    if (string.IsNullOrEmpty(key))
                        continue;

                    string lowerKey = key.ToLower().Trim();
                    string value = dictOriginalSetting[key];

                    if (lowerKey.StartsWith(keyHeadInfo + "hostinfo"))
                    {
                        string[] ipSetting = value.Split(',');
                        ipEPs = new IPEndPoint[ipSetting.Length];

                        for (int i = 0; i < ipEPs.Length; i++)
                        {

                            IPEndPoint _IPInfo = ParseHost(ipSetting[i]);
                            if (_IPInfo == null)
                                return null;

                            ipEPs[i] = _IPInfo;
                        }

                        actualBingoCount++;
                    }
                    else if (lowerKey.StartsWith(keyHeadInfo + "connectsocket_count"))
                    {
                        if (!int.TryParse(value, out maxConnectSocketCount))
                            return null;

                        actualBingoCount++;

                    }
                    else if (lowerKey.StartsWith(keyHeadInfo + "datasocket_count"))
                    {
                        if (!int.TryParse(value, out maxDataSocketCount))
                            return null;

                        actualBingoCount++;
                    }
                    else if (lowerKey.StartsWith(keyHeadInfo + "buffersize"))
                    {
                        if (!int.TryParse(value, out bufferSize))
                            return null;

                        actualBingoCount++;
                    }
                    else if (lowerKey.StartsWith(keyHeadInfo + "timeout"))
                    {
                        if (!int.TryParse(value, out timeOutByMS))
                            return null;

                        actualBingoCount++;
                    }
                    else if (lowerKey.StartsWith(keyHeadInfo + "keepalive"))
                    {

                        if (!bool.TryParse(value, out useKeepAlive))
                            return null;

                        actualBingoCount++;
                    }
                }

            }
            catch (Exception pickSettingErr) {

                LogManager.Log(string.Format("分析{0}配置错误", clientSettingType), pickSettingErr);
            }

            if (actualBingoCount.Equals(shouldBingoCount))
            {
                return new ClientSetting(ipEPs, numberSendCountPerConnection, maxConnectSocketCount,
                    maxDataSocketCount, bufferSize, receivePrefixLength, sendPrefixLength, opsToPreAllocate, timeOutByMS);
            }
            else
                throw new ArgumentNullException(keyHeadInfo + "ClientSettings");	            
        }

        /// <summary>
        /// socketsetting格式改为：
        /// 
        /// http:http
        /// hostinfo:...
        /// ...
        /// 
        /// log:log
        /// hostinfo:...
        /// ...
        /// 
        /// deploy:deploy
        /// hostinfo:...
        /// ...
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, string> GetKeyValueSettingII() {

            Dictionary<string, string> dictKeyValueSetting = new Dictionary<string, string>();

            try {

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

                string typeKey = string.Empty;

                for (int i = 0; i < lines.Length; i++)
                {
                    string[] configInfo = lines[i].Split(':');

                    if (configInfo.Length < 2)
                        continue;

                    if (configInfo[0].Equals(configInfo[1]))
                    {

                        typeKey = configInfo[0];

                        if (!IsValidTypeKey(typeKey))
                            typeKey = string.Empty;

                        continue;
                    }

                    if (string.IsNullOrEmpty(typeKey))
                        continue;

                    string entireKey = typeKey + "-" + configInfo[0];

                    if (dictKeyValueSetting.ContainsKey(entireKey))
                        dictKeyValueSetting[entireKey] = configInfo[1];
                    else
                        dictKeyValueSetting.Add(entireKey, configInfo[1]);
                }

            }
            catch (Exception getKeyValueErr) {

                LogManager.Log(string.Format("Parse {0} Error", path + fileName), getKeyValueErr);
                dictKeyValueSetting.Clear();
            }

            return dictKeyValueSetting.Count > 0 ? dictKeyValueSetting : null;
        }

        private static bool IsValidTypeKey(string typeKeyName) {            

            foreach (string enumName  in Enum.GetNames(typeof(ClientSettingType))) { 

                if(typeKeyName.ToLower().Equals(enumName.ToLower()))
                    return true;
            }

            return false;
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
                {
                    return null;
                }
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
