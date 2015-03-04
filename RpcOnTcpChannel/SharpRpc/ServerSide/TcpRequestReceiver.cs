using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpRpc.Interaction;
using SharpRpc.Logs;
using TcpFramework.Server;

namespace SharpRpc.ServerSide
{
    public class TcpRequestReceiver : IRequestReceiver
    {
        private readonly IIncomingRequestProcessor requestProcessor;
        private SocketListener listener;
        private bool isAlreadyStart = false;

        public TcpRequestReceiver(IIncomingRequestProcessor requestProcessor, ILogger logger) {

            this.requestProcessor = requestProcessor;

        }

        private byte[] Process(byte[] inputData) {

            Request requestData = null;

            if (!TryDecodeRequest(inputData, out requestData))
                return BitConverter.GetBytes((int)ResponseStatus.BadRequest);

            Task<Response> task = requestProcessor.Process(requestData);

            task.Wait();

            Response response = task.Result;

            byte[] statusByte = BitConverter.GetBytes((int)response.Status);
            byte[] result = new byte[statusByte.Length + response.Data.Length];

            Buffer.BlockCopy(statusByte, 0, result, 0, statusByte.Length);
            Buffer.BlockCopy(response.Data, 0, result, statusByte.Length, response.Data.Length);

            return result;
                        
        }

        public void Start(int port, int threads) {

            if (isAlreadyStart)
                return;

            listener = new SocketListener(Process);
            isAlreadyStart = true;
        }

        public void Stop() {

            listener.Stop();            
            isAlreadyStart = false;
        }
       
        private bool TryDecodeRequest(byte[] inputData, out Request requestData) {

            int uriDataLength = BitConverter.ToInt32(inputData, 0);
            string uriData = Encoding.UTF8.GetString(inputData, 4, uriDataLength);

            if (string.IsNullOrEmpty(uriData)) {

                requestData = null;
                return false;
            }

            Uri uri = new Uri(uriData);

            ServicePath servicePath;
            if (!ServicePath.TryParse(uri.LocalPath, out servicePath))
            {
                requestData = null;
                return false;
            }

            string scope = string.IsNullOrEmpty(uri.Query) || uri.Query.Length < 8 ? "" : uri.Query.Substring(7);

            byte[] data = new byte[inputData.Length - uriDataLength - 4];
            Buffer.BlockCopy(inputData, uriDataLength + 4, data, 0, inputData.Length - uriDataLength - 4);

            requestData = new Request(servicePath, scope, data);
            return true;           
        }
         
    }
}
