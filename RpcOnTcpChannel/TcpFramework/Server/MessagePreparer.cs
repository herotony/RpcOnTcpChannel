using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;


namespace TcpFramework.Server
{
    internal class MessagePreparer
    {
        internal static void GetDataToSend(SocketAsyncEventArgs e, byte[] sendData) {

            ServerUserToken userToken = (ServerUserToken)e.UserToken;

            //一个sessionid可能包括多个receivetransmissionid
            byte[] idByteArray = BitConverter.GetBytes(userToken.serverSession.ReceiveTransmissionId);
            byte[] sidByteArray = BitConverter.GetBytes(userToken.serverSession.SessionId);
            int lengthOfCurrentFeedbackMessage = idByteArray.Length +sidByteArray.Length+ sendData.Length;

            byte[] arrayOfBytesInPrefix = BitConverter.GetBytes(lengthOfCurrentFeedbackMessage);

            userToken.dataToSend = new byte[userToken.sendPrefixLength + lengthOfCurrentFeedbackMessage];

            Buffer.BlockCopy(arrayOfBytesInPrefix, 0, userToken.dataToSend, 0, userToken.sendPrefixLength);

            Buffer.BlockCopy(idByteArray, 0, userToken.dataToSend, userToken.sendPrefixLength, idByteArray.Length);
            Buffer.BlockCopy(sidByteArray, 0, userToken.dataToSend, userToken.sendPrefixLength + idByteArray.Length, sidByteArray.Length);

            Buffer.BlockCopy(sendData, 0, userToken.dataToSend, userToken.sendPrefixLength + idByteArray.Length+sidByteArray.Length, sendData.Length);

            userToken.sendBytesRemainingCount = userToken.sendPrefixLength + lengthOfCurrentFeedbackMessage;
            userToken.bytesSentAlreadyCount = 0;
        }

    }
}
