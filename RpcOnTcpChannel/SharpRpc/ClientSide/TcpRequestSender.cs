using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpRpc.Interaction;
using TcpFramework.Client;

namespace SharpRpc.ClientSide
{
    public class TcpRequestSender :IRequestSender
    {
        public Task<Response> SendAsync(string host, int port, Request request, int? timeoutMilliseconds) {

            throw new NotImplementedException();
        }

        public Response Send(string host, int port, Request request, int? timeoutMilliseconds) {

            var uri = string.Format("tcp://{0}:{1}/{2}?scope={3}", host, port, request.Path, request.ServiceScope);

            byte[] uriByte = Encoding.UTF8.GetBytes(uri);
            byte[] uriLenInfoByte = BitConverter.GetBytes(uriByte.Length);
            byte[] sendData = new byte[uriLenInfoByte.Length + uriByte.Length + request.Data.Length];

            Buffer.BlockCopy(uriLenInfoByte, 0, sendData, 0, uriLenInfoByte.Length);
            Buffer.BlockCopy(uriByte, 0, sendData, uriLenInfoByte.Length, uriByte.Length);
            Buffer.BlockCopy(request.Data, 0, sendData, uriLenInfoByte.Length + uriByte.Length, request.Data.Length);

            ClientSocketManager socketManager = new ClientSocketManager();
            string result = string.Empty;

            byte[] returnData = socketManager.SendRequest(sendData, ref result);

            if (result.Equals("ok") && returnData != null)
            {
                int tranId = BitConverter.ToInt32(returnData, 0);
                int sid = BitConverter.ToInt32(returnData, 4);
                int responseStatusValue = BitConverter.ToInt32(returnData, 8);

                ResponseStatus responseStatus = (ResponseStatus)responseStatusValue;

                if (returnData.Length > 12)
                {
                    byte[] responseData = new byte[returnData.Length - 12];
                    Buffer.BlockCopy(returnData, 12, responseData, 0, returnData.Length - 12);

                    return new Response(responseStatus, responseData);
                }

                return new Response(responseStatus, new byte[0]);
            }
            else if (result.Equals("timeout"))
            {
                return new Response(ResponseStatus.InternalServerError, new byte[0]);
            }
            else
                return new Response(ResponseStatus.BadRequest, new byte[0]);
            
        }
    }
}
