using System;
using System.Collections.Generic;

namespace TcpFramework.Client
{
    internal class SendDataHolder
    {        
        //即每个元素是一次完整请求，相当于一个连接一次处理N条请求
        internal List<Message> ArrayOfMessageToSend = new List<Message>();
        internal bool OnHeartBeatStatus;
        
        //用于逐个提取List中消息
        internal int NumberOfMessageHadSent { get; set; }

        internal void SetSendMessage(List<Message> theArrayOfMessagesToSend)
        {
            if (theArrayOfMessagesToSend != null && theArrayOfMessagesToSend.Count > 0)
            {
                this.ArrayOfMessageToSend = theArrayOfMessagesToSend;
            }

            this.OnHeartBeatStatus = false;
        }       
    }
}
