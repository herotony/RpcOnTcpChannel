using System;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class PrefixHandler
    {
        internal static int  HandlePrefix(SocketAsyncEventArgs arg,DataUserToken userToken,int remainingBytesToProcess){

            if (userToken.receivedPrefixBytesDoneCount.Equals(0)) {

                userToken.byteArrayForPrefix = new byte[userToken.receivePrefixLength];
            }

            if (remainingBytesToProcess > (userToken.receivePrefixLength - userToken.receivedPrefixBytesDoneCount))
            {
                //接收数据足够多，甚至已经包含了具体的内容数据
                Buffer.BlockCopy(arg.Buffer, userToken.receiveMessageOffset - userToken.receivePrefixLength + userToken.receivedPrefixBytesDoneCount, userToken.byteArrayForPrefix, userToken.receivedPrefixBytesDoneCount, userToken.receivePrefixLength - userToken.receivedPrefixBytesDoneCount);

                remainingBytesToProcess = remainingBytesToProcess - userToken.receivePrefixLength + userToken.receivedPrefixBytesDoneCount;
                userToken.recPrefixBytesDoneThisOp = userToken.receivePrefixLength - userToken.receivedPrefixBytesDoneCount;
                userToken.receivedPrefixBytesDoneCount = userToken.receivePrefixLength;

                userToken.lengthOfCurrentIncomingMessage = BitConverter.ToInt32(userToken.byteArrayForPrefix, 0);
            }
            else {

                //接收数据太小，不足以得到数据长度头信息，需要再次接收补全
                Buffer.BlockCopy(arg.Buffer, userToken.receiveMessageOffset - userToken.receivePrefixLength + userToken.receivedPrefixBytesDoneCount, userToken.byteArrayForPrefix, userToken.receivedPrefixBytesDoneCount, remainingBytesToProcess);

                userToken.recPrefixBytesDoneThisOp = remainingBytesToProcess;
                userToken.receivedPrefixBytesDoneCount += remainingBytesToProcess;
                remainingBytesToProcess = 0;
            }

            if (remainingBytesToProcess == 0)
            {
                //必须的处理步骤！确保后续数据的正确处理
                userToken.receiveMessageOffset = userToken.receiveMessageOffset - userToken.recPrefixBytesDoneThisOp;
                userToken.recPrefixBytesDoneThisOp = 0;
            }

            return 0;
        }
    }
}
