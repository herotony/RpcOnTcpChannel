using System;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class MessageHandler
    {
        internal static bool HandleMessage(SocketAsyncEventArgs arg, DataUserToken userToken, int remainingBytesToProcess) {

            bool incomingTcpMessageIsReady = false;

            if (userToken.receivedMessageBytesDoneCount == 0)
            {
                userToken.dataMessageReceived = new Byte[userToken.lengthOfCurrentIncomingMessage];
            }

            if (remainingBytesToProcess + userToken.receivedMessageBytesDoneCount == userToken.lengthOfCurrentIncomingMessage)
            {
                // Write/append the bytes received to the byte array in the 
                // DataHolder object that we are using to store our data.
                Buffer.BlockCopy(arg.Buffer, userToken.receiveMessageOffset, userToken.dataMessageReceived, userToken.receivedMessageBytesDoneCount, remainingBytesToProcess);

                incomingTcpMessageIsReady = true;
            }
            else {

                Buffer.BlockCopy(arg.Buffer, userToken.receiveMessageOffset, userToken.dataMessageReceived, userToken.receivedMessageBytesDoneCount, remainingBytesToProcess);

                userToken.receiveMessageOffset = userToken.receiveMessageOffset - userToken.recPrefixBytesDoneThisOp;
                userToken.receivedMessageBytesDoneCount += remainingBytesToProcess;
            }

            return incomingTcpMessageIsReady;
        }
    }
}
