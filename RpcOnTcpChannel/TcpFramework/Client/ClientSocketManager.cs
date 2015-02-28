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
        //缓存返回数据
        private static ConcurrentDictionary<int, byte[]> dictResult = new ConcurrentDictionary<int, byte[]>();

        //关键是为了记录发送到返回的起始时间点，便于查看耗时
        private static ConcurrentQueue<Message> SendingMessages = new ConcurrentQueue<Message>();

        //相当于83886080 个请求，10兆空间处理8千万请求/天，不是并发哦，是一天一个前端的累计访问量
        private static BitArray arrTokenId = new BitArray(new byte[10 * 1024 * 1024]);

        private static int msgTokenId = 0;
        private static int timeOutByMS = 1000;//超时设置，单位毫秒

        //定时清理参数
        private static int ClearTime = 3;
        private static bool IsAlreadyClear = false;
        //查询并发
        private static int concurrentCount = 0;

        //核心部分！
        private static BufferManager bufferManager;

        //开启两个线程，一个负责清零，一个负责发送
        private static Thread threadClear;
        private static Thread threadSending;

        //最终要修改为读取配置
        private static ClientSetting clientSetting;

        private static SocketAsyncEventArgPool poolOfRecSendEventArgs;
        private static SocketAsyncEventArgPool poolOfConnectEventArgs;

        private static ClientSocketProcessor processor;

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
            poolOfConnectEventArgs = new SocketAsyncEventArgPool(clientSetting.maxSimultaneousConnectOps);

            //用于负责在建立好的连接上传输数据，涉及buffermanager，目前测试100～200个足够！这部分目前不支持动态增加！
            //因其buffermanager是事先分配好的一大块连续的固定内存区域，强烈建议不再更改，需要做好的就是事先的大小评估。
            poolOfRecSendEventArgs = new SocketAsyncEventArgPool(clientSetting.numberOfSaeaForRecSend);

            //实际负责处理相关传输数据的关键核心类
            processor = new ClientSocketProcessor(poolOfConnectEventArgs, poolOfRecSendEventArgs, clientSetting.maxSimultaneousConnectOps, clientSetting.bufferSize, clientSetting.numberOfMessagesPerConnection, clientSetting.receivePrefixLength);

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

                //用于传递待发送的数据，一旦完成发送可以重新new一个。
                receiveSendToken.CreateNewSendDataHolder();
                eventArgObjectForPool.UserToken = receiveSendToken;

                poolOfRecSendEventArgs.Push(eventArgObjectForPool);
            }

            //接收返回结果
            processor.ReceiveDataCompleteCallback = TryAdd;

            threadClear = new Thread(new ThreadStart(RunClear));
            threadClear.IsBackground = true;//进程结束则直接干掉本线程即可，无需等待!
            threadClear.Start();

            threadSending = new Thread(new ThreadStart(SendMessageOverAndOver));
            threadSending.IsBackground = true;
            threadSending.Start();

            sw.Stop();

            LogManager.Log(string.Format("SocketClient Init by FirstInvoke Completed! ConsumeTime:{0} ms", sw.ElapsedMilliseconds));

        }

        //唯一对外发送接口，在相关场景中，该方法的调用方应该考虑使用线程池或Task来控制并发量！
        public static byte[] SendRequest(byte[] sendData, ref string message)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            int _tokenId = GetNewTokenId();

            Message _message = new Message();
            _message.TokenId = _tokenId;
            _message.Content = sendData;

            //数据写入数据池
            SendingMessages.Enqueue(_message);

            byte[] retData;
            bool isTimeOut = false;

            //wait result...
            while (!IsIOComplete(_tokenId))
            {
                if (sw.ElapsedMilliseconds > timeOutByMS)
                {
                    sw.Stop();
                    isTimeOut = true;
                    break;
                }

                //貌似更有效率！得益于2003server后的win api sleep版本修改
                Thread.Sleep(0);
            }

            if (isTimeOut)
            {
                message = string.Format("Try get retdata timeout on MsgTokenId:{0}! consumetime:{1} ms", _tokenId, sw.ElapsedMilliseconds);
                return null;
            }

            bool getResult = TryGetResult(_tokenId, out retData);

            sw.Stop();

            if (!getResult)
                message = string.Format("Try get retdata from dictionary failed on MsgTokenId:{0}! consumetime:{1} ms", _tokenId, sw.ElapsedMilliseconds);
            else
                message = string.Format("get retdata sucessfully! MsgTokenId:{0} reddata length:{1} consumetime:{2} ms", _tokenId, retData.Length, sw.ElapsedMilliseconds);

            return retData;
        }

        //循环发送线程方法
        private static void SendMessageOverAndOver()
        {
            List<Message> listSend = new List<Message>();

            bool dequeueOk = false;

            Message firstMsg = null;
            Message msg = null;

            while (true)
            {
                try
                {                   
                    bool isFirstInPerLoop = true;

                    dequeueOk = SendingMessages.TryDequeue(out firstMsg);

                    while (dequeueOk)
                    {
                        if (isFirstInPerLoop && firstMsg != null)
                        {
                            isFirstInPerLoop = false;
                            firstMsg.StartTime = DateTime.Now;

                            //待转交saea
                            listSend.Add(firstMsg);
                        }
                        else
                        {
                            if (msg != null)
                            {
                                msg.StartTime = DateTime.Now;

                                //待转交saea
                                listSend.Add(msg);
                            }
                        }

                        if (listSend.Count > 0)
                        {                          
                            if (listSend.Count >= clientSetting.numberOfMessagesPerConnection)
                            {
                                processor.SendMessage(listSend, clientSetting.serverEndPoint);
                                listSend = new List<Message>();
                            }
                        }

                        dequeueOk = SendingMessages.TryDequeue(out msg);
                    }

                    //确保长连接多发时，不会因为小于多发的数量而此时队列没数据了导致的发送丢失
                    if (listSend.Count > 0)
                    {
                        processor.SendMessage(listSend, clientSetting.serverEndPoint);
                        listSend = new List<Message>();
                    }

                }
                catch (InvalidOperationException e)
                {
                    LogManager.Log(string.Format("SendingMessages queue maybe empty(count:{0})", SendingMessages.Count), e);
                }
                catch (Exception otherError)
                {
                    LogManager.Log("SendMessageOverAndOver occur Error!", otherError);
                }

                Thread.Sleep(0);
            }
        }

        internal static bool TryAdd(int tokenId, byte[] retData)
        {
            byte[] copyData = new byte[retData.Length];

            if (retData.Length > 0)
                Buffer.BlockCopy(retData, 0, copyData, 0, retData.Length);

            arrTokenId[tokenId] = true;

            bool addRet = dictResult.TryAdd(tokenId, copyData);
            if (!addRet)
            {
                //再试一次
                addRet = dictResult.TryAdd(tokenId, copyData);

                if(!addRet)
                    LogManager.Log(string.Empty, new Exception(string.Format("TryAdd retData[{0} byte] Failed on MsgTokenId:{1}", copyData.Length, tokenId)));
            }

            return addRet;
        }

        private static bool TryGetResult(int tokenId, out byte[] retData)
        {
            bool tryRemove = dictResult.TryRemove(tokenId, out retData);

            if (!tryRemove)
            {
                //留日志即可...
                LogManager.Log(string.Format("TryRemove MsgTokenId:{0} failed!", tokenId));
            }

            return retData != null && retData.Length > 0;
        }

        private static bool IsIOComplete(int tokenId)
        {
            return arrTokenId[tokenId];
        }

        internal static int GetNewTokenId()
        {
            return Interlocked.Increment(ref msgTokenId);
        }

        //凌晨清零!
        private static void RunClear()
        {
            int retryCount = 0;

            while (true)
            {
                if (IsAlreadyClear)
                {
                    if (!DateTime.Now.Hour.Equals(ClearTime))
                        IsAlreadyClear = false;
                }

                if (!IsAlreadyClear && DateTime.Now.Hour.Equals(ClearTime))
                {
                    if (concurrentCount < 3 || retryCount > 10)
                    {
                        //清理							
                        ClearAllTokenId();
                        IsAlreadyClear = true;
                        retryCount = 0;
                    }
                    else
                        retryCount++;
                }

                Thread.Sleep(10000);
            }
        }

        //用于凌晨某时刻清零...
        private static void ClearAllTokenId()
        {
            msgTokenId = 0;
            arrTokenId.SetAll(false);
            dictResult.Clear();

            LogManager.Log("ClearAllTokenId Complete Successfully!");
        }
    }
}
