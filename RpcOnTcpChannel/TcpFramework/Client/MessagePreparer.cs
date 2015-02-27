using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace TcpFramework.Client
{
    internal class MessagePreparer
    {
        internal static void GetDataToSend(SocketAsyncEventArgs e)
        {
            ClientUserToken theUserToken = (ClientUserToken)e.UserToken;
            SendDataHolder dataHolder = theUserToken.sendDataHolder;

            //In this example code, we will  
            //prefix the message with the length of the message. So we put 2 
            //things into the array.
            // 1) prefix,
            // 2) the message.

            //Determine the length of the message that we will send.
            int lengthOfCurrentOutgoingMessage = dataHolder.ArrayOfMessageToSend[dataHolder.NumberOfMessageHadSent].Content.Length;

            //convert the message to byte array
            byte[] arrayOfBytesInMessage = dataHolder.ArrayOfMessageToSend[dataHolder.NumberOfMessageHadSent].Content;

            //So, now we convert the length integer into a byte array.
            //Aren't byte arrays wonderful? Maybe you'll dream about byte arrays tonight!
            byte[] arrayOfBytesInPrefix = BitConverter.GetBytes(lengthOfCurrentOutgoingMessage);

            //Create the byte array to send.
            theUserToken.dataToSend = new Byte[theUserToken.sendPrefixLength + lengthOfCurrentOutgoingMessage];

            theUserToken.messageTokenId = dataHolder.ArrayOfMessageToSend[dataHolder.NumberOfMessageHadSent].TokenId;

            //Now copy the 2 things to the theUserToken.dataToSend.
            Buffer.BlockCopy(arrayOfBytesInPrefix, 0, theUserToken.dataToSend, 0, theUserToken.sendPrefixLength);
            Buffer.BlockCopy(arrayOfBytesInMessage, 0, theUserToken.dataToSend, theUserToken.sendPrefixLength, lengthOfCurrentOutgoingMessage);

            theUserToken.sendBytesRemainingCount = theUserToken.sendPrefixLength + lengthOfCurrentOutgoingMessage;
            theUserToken.bytesSentAlreadyCount = 0;
        }

    }
}
