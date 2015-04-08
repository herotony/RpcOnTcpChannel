using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpManager
{
    public class Server
    {
        public static string GetRequestString(byte[] inputData, ref string command) {

            int commandLength = BitConverter.ToInt32(inputData, 0);
            command = Encoding.UTF8.GetString(inputData, 4,commandLength);

            return Encoding.UTF8.GetString(inputData, 4 + commandLength, inputData.Length - 4 - commandLength);
        }
    }
}
