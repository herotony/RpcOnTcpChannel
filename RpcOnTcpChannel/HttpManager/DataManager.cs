using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpManager
{
    public class DataManager
    {
        public static byte[] GetSendData(Queue<ClientData> queueData, int totalLength)
        {

            byte[] sendData = new byte[totalLength];
            int startIndex = 0;

            while (queueData.Count() > 0)
            {

                ClientData data = queueData.Dequeue();

                if (data == null)
                    continue;

                Buffer.BlockCopy(data.ByteOfContentLength, 0, sendData, startIndex, data.ByteOfContentLength.Length);
                Buffer.BlockCopy(data.ByteOfContent, 0, sendData, startIndex + data.ByteOfContentLength.Length, data.ByteOfContent.Length);

                startIndex = data.GetNewStartIndex(startIndex);
            }

            return sendData;
        }

        public static int PushClientDataToQueueAndFeedbackLength(Queue<ClientData> queueData, string inputData)
        {

            byte[] cntLength;
            byte[] cnt = ToUTF8ByteData(inputData, out cntLength);

            ClientData data = new ClientData();
            data.ByteOfContent = cnt;
            data.ByteOfContentLength = cntLength;

            queueData.Enqueue(data);

            return cntLength.Length + cnt.Length;
        }

        private static byte[] ToUTF8ByteData(string inputData, out byte[] byteLength)
        {

            byte[] data = Encoding.UTF8.GetBytes(inputData);
            byteLength = BitConverter.GetBytes(data.Length);

            return data;
        }

        public static string[] DecodeByteData(byte[] inputData) {

            int startIndex = 0;

            int referenceCount = int.Parse(DecodeToString(inputData, ref startIndex));

            string[] arrValue = new string[referenceCount];

            int index = 0;

            while (referenceCount > 0) {

                arrValue[index] = DecodeToString(inputData, ref startIndex);

                index++;
                referenceCount--;
            }

            return arrValue;
        }

        private static string DecodeToString(byte[] inputData,ref int startIndex) {

            int _startIndex = startIndex;
            int unitDataLength = BitConverter.ToInt32(inputData, startIndex);
            startIndex += unitDataLength + 4;

            return Encoding.UTF8.GetString(inputData, _startIndex + 4, unitDataLength);
        }
    }

    public class ClientData
    {
        internal byte[] ByteOfContentLength { get; set; }
        internal byte[] ByteOfContent { get; set; }

        internal int GetNewStartIndex(int startIndex)
        {
            return ByteOfContentLength.Length + ByteOfContent.Length + startIndex;
        }
    }
}
