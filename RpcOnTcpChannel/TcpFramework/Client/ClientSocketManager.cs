using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Net;

using TcpFramework.Common;

namespace TcpFramework.Client
{
    public class ClientSocketManager
    {              
        private static int msgTokenId = 0;
        private static int timeOutByMS = 500;//超时设置，单位毫秒        

        //核心部分！
        private static BufferManager bufferManager;       

        //最终要修改为读取配置
        private static ClientSetting clientSetting;

        private static SocketAsyncEventArgPool poolOfRecSendEventArgs;
        private static SocketAsyncEventArgPool poolOfConnectEventArgs;

        private static  ClientSocketProcessor processor;
        private static Random rand = new Random();
        private static int ServerCount = 0;
        private static object lockForPickObject = new object();
        private static int prevPickIndex = 0;
        private static bool isFirstPickOver = false;
        private static int concurrentRequestCount = 0;       

        public delegate byte[] PickResult(int tokenId);

        static ClientSocketManager() {

            Init();
        }

        private static void Init() {

            Stopwatch sw = new Stopwatch();

            sw.Start();

            clientSetting = ReadConfigFile.GetClientSetting();

            ServerCount = clientSetting.serverEndPoints.Length;

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
            processor = new ClientSocketProcessor(poolOfConnectEventArgs, poolOfRecSendEventArgs, clientSetting.numberOfSaeaForRecSend, clientSetting.bufferSize, clientSetting.numberOfMessagesPerConnection, clientSetting.receivePrefixLength,clientSetting.useKeepAlive,"bizclient");

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

                int _tokenId = GetNewTokenId();

                Message _message = new Message();
                _message.TokenId = _tokenId;
                _message.Content = sendData;
                this.messageTokenId = _tokenId;
                
                processor.ReceiveFeedbackDataComplete += Processor_ReceiveFeedbackDataComplete;

                List<Message> list = new List<Message>();
                list.Add(_message);
                
                //System.Net.IPEndPoint _serverEndPoint = clientSetting.serverEndPoints[rand.Next(ServerCount)];
                IPEndPoint _serverEndPoint = loopPickServerEndPoint();

                Interlocked.Increment(ref concurrentRequestCount);
                processor.simplePerf.PerfClientRequestTotalCounter.Increment();

                SendStatus  sendStatus = processor.SendMessage(list, _serverEndPoint,concurrentRequestCount);

                switch (sendStatus) {

                    case SendStatus.OK_ON_OPENNEW:
                    case SendStatus.OK_ON_STEPONE_REUSE:
                    case SendStatus.OK_ON_STEPTWO_REUSE:

                        if (manualResetEvent.WaitOne(timeOutByMS))
                        {                            
                            message = "socket:ok";
                            processor.simplePerf.PerfClientRequestSuccessCounter.Increment();
                            return result;
                        }

                        message = "socket:timeout";
                        break;
                    case SendStatus.CONNECTION_EXHAUST:
                        message = "socket:connection exhaust";
                        break;
                    case SendStatus.TOOMANY_REQUEST:
                        message = "socket:too many request";
                        break;
                };

                processor.simplePerf.PerfClientRequestFailCounter.Increment();         
            }
            catch(Exception sendErr) {

                message = "err:"+sendErr.Message + "\r\nstackTrace:" + sendErr.StackTrace;
            }
            finally {
               
                try
                {
                    Interlocked.Decrement(ref concurrentRequestCount);
                    processor.ReceiveFeedbackDataComplete -= this.Processor_ReceiveFeedbackDataComplete;                    
                }
                finally { }                
            }
           
            return null;
        }      

        private void Processor_ReceiveFeedbackDataComplete(object sender, ReceiveFeedbackDataCompleteEventArg e) {

            if (!e.MessageTokenId.Equals(this.messageTokenId))
                return;

            try {

                if (e.FeedbackData != null && e.FeedbackData.Length > 0)
                {
                    result = new byte[e.FeedbackData.Length];
                    Buffer.BlockCopy(e.FeedbackData, 0, result, 0, e.FeedbackData.Length);
                }
            }
            catch(Exception feedbackErr) {

                LogManager.Log(string.Format("{0} occur error", e.MessageTokenId), feedbackErr);
            }
            finally {
                
                manualResetEvent.Set();   
            }                                       
        }

        private IPEndPoint loopPickServerEndPoint() {            
            
            lock (lockForPickObject) {

                if (!isFirstPickOver)
                {
                    isFirstPickOver = true;
                    prevPickIndex = 0;

                    return clientSetting.serverEndPoints[0];
                }
                else {

                    int currentIndex = prevPickIndex + 1;
                    if (currentIndex >= ServerCount)
                    {
                        prevPickIndex = 0;
                        return clientSetting.serverEndPoints[0];
                    }
                    else
                    {
                        prevPickIndex = currentIndex;
                        return clientSetting.serverEndPoints[currentIndex];
                    }
                }
            }            
        }

        private static int GetNewTokenId()
        {
            return Interlocked.Increment(ref msgTokenId);
        }       
    }
}
