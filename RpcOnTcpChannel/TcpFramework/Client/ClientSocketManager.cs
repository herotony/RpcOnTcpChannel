using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using TcpFramework.Common;

namespace TcpFramework.Client
{
    public class ClientSocketManager
    {              
        private static int msgTokenId = 0;
        private static int timeOutByMS = 1000;//超时设置，单位毫秒        

        //核心部分！
        private static BufferManager bufferManager;       

        //最终要修改为读取配置
        private static ClientSetting clientSetting;

        private static SocketAsyncEventArgPool poolOfRecSendEventArgs;
        private static SocketAsyncEventArgPool poolOfConnectEventArgs;

        private static  ClientSocketProcessor processor;

        //private static Semaphore maxCurrentSignal = new Semaphore(10,10);

        public delegate byte[] PickResult(int tokenId);

        static ClientSocketManager() {

            Init();
        }

        private static void Init() {

            Stopwatch sw = new Stopwatch();

            sw.Start();

            clientSetting = ReadConfigFile.GetClientSetting();

            timeOutByMS = clientSetting.timeOutByMS;            

            //初始化后，不可更改！
            bufferManager = new BufferManager(clientSetting.bufferSize * clientSetting.opsToPreAllocate * clientSetting.numberOfSaeaForRecSend, clientSetting.bufferSize * clientSetting.opsToPreAllocate);
            bufferManager.InitBuffer();

            //用于负责建立连接的saea，无关buffermanager，10个足够！这部分实际在ClientSocketProcessor中可以动态增加
            //poolOfConnectEventArgs = new SocketAsyncEventArgPool(clientSetting.maxSimultaneousConnectOps);
            poolOfConnectEventArgs = new SocketAsyncEventArgPool();

            //用于负责在建立好的连接上传输数据，涉及buffermanager，目前测试100～200个足够！这部分目前不支持动态增加！
            //因其buffermanager是事先分配好的一大块连续的固定内存区域，强烈建议不再更改，需要做好的就是事先的大小评估。
            //poolOfRecSendEventArgs = new SocketAsyncEventArgPool(clientSetting.numberOfSaeaForRecSend);
            poolOfRecSendEventArgs = new SocketAsyncEventArgPool();

            //实际负责处理相关传输数据的关键核心类
            processor = new ClientSocketProcessor(poolOfConnectEventArgs, poolOfRecSendEventArgs, clientSetting.numberOfSaeaForRecSend, clientSetting.bufferSize, clientSetting.numberOfMessagesPerConnection, clientSetting.receivePrefixLength,clientSetting.useKeepAlive);

            //由于不涉及buffermanager，可动态增长
            for (int i = 0; i < clientSetting.maxSimultaneousConnectOps; i++)
            {
                SocketAsyncEventArgs connectEventArg = new SocketAsyncEventArgs();
                connectEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(processor.IO_Completed);

                //关键负责标识saea和更关键的传输待发送的数据给传输用的saea。
                ConnectOpUserToken theConnectingToken = new ConnectOpUserToken();
                connectEventArg.UserToken = theConnectingToken;

                poolOfConnectEventArgs.Push(connectEventArg);
            }

            //涉及buffermanager，不可动态增长，需事先评估即可，负责实际的数据传输
            for (int i = 0; i < clientSetting.numberOfSaeaForRecSend; i++)
            {
                SocketAsyncEventArgs eventArgObjectForPool = new SocketAsyncEventArgs();

                //事先为每个saea分配固定不变的内存位置！
                bufferManager.SetBuffer(eventArgObjectForPool);

                eventArgObjectForPool.Completed += new EventHandler<SocketAsyncEventArgs>(processor.IO_Completed);

                ClientUserToken receiveSendToken = new ClientUserToken(eventArgObjectForPool.Offset, eventArgObjectForPool.Offset + clientSetting.bufferSize, clientSetting.receivePrefixLength, clientSetting.sendPrefixLength);

                //用于传递待发送的数据，一旦完成发送必须重新new一个。
                receiveSendToken.CreateNewSendDataHolder();
                eventArgObjectForPool.UserToken = receiveSendToken;

                poolOfRecSendEventArgs.Push(eventArgObjectForPool);
            }                      

            sw.Stop();

            LogManager.Log(string.Format("SocketClient Init by FirstInvoke Completed! ConsumeTime:{0} ms", sw.ElapsedMilliseconds));

        }

        private int messageTokenId = 0;        
        private byte[] result = null;        
        private ManualResetEvent manualResetEvent = new ManualResetEvent(false);

        public byte[] SendRequest(byte[] sendData, ref string message) {

            try {

                //maxCurrentSignal.WaitOne();

                int _tokenId = GetNewTokenId();

                Message _message = new Message();
                _message.TokenId = _tokenId;
                _message.Content = sendData;
                this.messageTokenId = _tokenId;
                
                processor.ReceiveFeedbackDataComplete += Processor_ReceiveFeedbackDataComplete;

                List<Message> list = new List<Message>();
                list.Add(_message);
                processor.SendMessage(list, clientSetting.serverEndPoint);

                if (manualResetEvent.WaitOne(timeOutByMS))
                {
                    message = "ok";                    
                    return result;
                }

                message = "timeout";                
            }
            catch { }
            finally {

               // maxCurrentSignal.Release();
                processor.ReceiveFeedbackDataComplete -= this.Processor_ReceiveFeedbackDataComplete;
            }
           
            return null;
        }      

        private void Processor_ReceiveFeedbackDataComplete(object sender, ReceiveFeedbackDataCompleteEventArg e) {

            if (!e.MessageTokenId.Equals(this.messageTokenId))
                return;

            if (e.FeedbackData != null && e.FeedbackData.Length > 0) {

                result = new byte[e.FeedbackData.Length];
                Buffer.BlockCopy(e.FeedbackData, 0, result, 0, e.FeedbackData.Length);
            }

            manualResetEvent.Set();                   
        }                   

        private static int GetNewTokenId()
        {
            return Interlocked.Increment(ref msgTokenId);
        }       
    }
}
