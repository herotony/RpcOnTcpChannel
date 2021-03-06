﻿using System;

namespace TcpFramework.Common
{
    internal class DataUserToken
    {
        //用于计算当前对应的socket处理耗时使用
        internal DateTime startTime;

        //在MesssagePreparer中提取待发送的消息针对此usertoken的messageTokenId进行修正赋值
        internal int messageTokenId = 0;

        internal readonly int bufferOffsetReceive;
        internal readonly int permanentReceiveMessageOffset;        
        internal readonly int receivePrefixLength;
        internal readonly int bufferOffsetSend;
        internal readonly int sendPrefixLength;

        internal int lengthOfCurrentIncomingMessage;
        internal int receiveMessageOffset;
        internal int receivedMessageBytesDoneCount = 0;

        internal byte[] byteArrayForPrefix;
        internal int receivedPrefixBytesDoneCount = 0;
        internal int recPrefixBytesDoneThisOp = 0;

        internal byte[] dataToSend;
        internal int bytesSentAlreadyCount;
        internal int sendBytesRemainingCount;

        internal byte[] dataMessageReceived;

        internal DataUserToken(int receiveOffset, int sendOffset, int receivePrefixLength, int sendPrefixLength) {


            this.bufferOffsetReceive = receiveOffset;
            this.bufferOffsetSend = sendOffset;
            this.receivePrefixLength = receivePrefixLength;
            this.sendPrefixLength = sendPrefixLength;
            //会不断修正，permanentReceiveMessageOffset则是用于清零归位使用
            this.receiveMessageOffset = receiveOffset + receivePrefixLength;
            this.permanentReceiveMessageOffset = this.receiveMessageOffset;  

        }

        internal void Reset()
        {
            this.messageTokenId = 0;
            this.receivedPrefixBytesDoneCount = 0;
            this.receivedMessageBytesDoneCount = 0;
            this.recPrefixBytesDoneThisOp = 0;
            this.receiveMessageOffset = this.permanentReceiveMessageOffset;
        }
    }
}
