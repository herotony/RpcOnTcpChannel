using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

using TcpFramework.Common;


namespace TcpFramework.Client
{
    public enum SendStatus { OK_ON_STEPONE_REUSE, OK_ON_STEPTWO_REUSE, OK_ON_OPENNEW, CONNECTION_EXHAUST, TOOMANY_REQUEST };

    internal class ClientSocketProcessor
    {       
        public event EventHandler<ReceiveFeedbackDataCompleteEventArg> ReceiveFeedbackDataComplete;                       

        private bool supportKeepAlive = false;
        private Dictionary<string,SocketAsyncEventArgPool> dictPoolOfHeartBeatRecSendEventArgs;
        private ManualResetEvent cleanSignal = new ManualResetEvent(false);
        private Thread thHeartBeat;

        internal SimplePerformanceCounter simplePerf;
        internal string specialNameForSimplePerf = string.Empty;
        private SocketAsyncEventArgPool poolOfRecSendEventArgs;
        private SocketAsyncEventArgPool poolOfConnectEventArgs;        
           
        //允许的最大并发连接数
        private Semaphore maxConcurrentConnection;
        private object lockConcurrentStepOne = new object();
        private object lockConcurrentStepTwo = new object();
        private int maxConnectionCount = 0;
        private int currentConnectionCount = 0;
      
        //每个连接最多能发送的消息数量，一般为一次一条
        private int numberMessagesOfPerConnection;
        //一般为整型int的四个字节
        private int prefixHandleLength;
        private int bufferSize;
             
        public ClientSocketProcessor(SocketAsyncEventArgPool connectPool, SocketAsyncEventArgPool recSendPool, int maxRecSendConnection, int bufferSize, int numberMessageOfPerConnection, int prefixHandleLength,bool supportKeepAlive = false,string specialName="") {

            this.poolOfConnectEventArgs = connectPool;
            this.poolOfRecSendEventArgs = recSendPool;

            this.specialNameForSimplePerf = specialName;
            this.simplePerf = new SimplePerformanceCounter(true,false,specialName);

            this.maxConnectionCount = maxRecSendConnection;
            this.maxConcurrentConnection = new Semaphore(maxRecSendConnection, maxRecSendConnection);

            this.bufferSize = bufferSize;
            this.numberMessagesOfPerConnection = numberMessageOfPerConnection;
            this.prefixHandleLength = prefixHandleLength;

            this.supportKeepAlive = supportKeepAlive;           
            
            if (this.supportKeepAlive)
            {
                dictPoolOfHeartBeatRecSendEventArgs = new Dictionary<string, SocketAsyncEventArgPool>();
                thHeartBeat = new Thread(new ThreadStart(RunHeartBeat));
                thHeartBeat.IsBackground = true;
                thHeartBeat.Start();
            }
        }

        internal SimplePerformanceCounter GetSimplePerf() {

            if (this.simplePerf == null)
            {
                lock (this)
                {
                    if (this.simplePerf == null)
                    {
                        this.simplePerf = new SimplePerformanceCounter(true, false, specialNameForSimplePerf);

                        if (this.simplePerf == null)
                            throw new ArgumentNullException("simplePerf","renew client simplePerf instance failed!");
                    }  
                }
            }

            return this.simplePerf;
        }

        internal SendStatus SendMessage(List<Message> messages, IPEndPoint serverEndPoint,int currentRequestCount)
        {           
            if (supportKeepAlive) {

                if (ReuseHeartBeatSAEA(messages, string.Format("{0}_{1}",serverEndPoint.Address,serverEndPoint.Port)))
                {                    
                    return SendStatus.OK_ON_STEPONE_REUSE;
                }               
            }

            //到达配置限制数量，立即返回防止IIS线程越开越多进入假死状态。
            lock (lockConcurrentStepOne) {

                if ((maxConnectionCount - currentConnectionCount) < 1)
                    return SendStatus.CONNECTION_EXHAUST;
                else if (currentRequestCount > maxConnectionCount)
                    return SendStatus.TOOMANY_REQUEST;
            }
                       
            maxConcurrentConnection.WaitOne();

            if (supportKeepAlive) {

                if (ReuseHeartBeatSAEA(messages, string.Format("{0}_{1}",serverEndPoint.Address,serverEndPoint.Port)))
                {
                    return SendStatus.OK_ON_STEPTWO_REUSE;
                }  
            }

            //到达配置限制数量，立即返回防止IIS线程越开越多进入假死状态。
            lock (lockConcurrentStepTwo)
            {
                if ((maxConnectionCount - currentConnectionCount) < 1)
                    return SendStatus.CONNECTION_EXHAUST;
                else if (currentRequestCount > maxConnectionCount)
                    return SendStatus.TOOMANY_REQUEST;
            }            

            SocketAsyncEventArgs connectEventArgs = this.poolOfConnectEventArgs.Pop();

            //or make a new one.            
            if (connectEventArgs == null)
            {
                connectEventArgs = new SocketAsyncEventArgs();
                connectEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

                ConnectOpUserToken theConnectingToken = new ConnectOpUserToken();
                theConnectingToken.ArrayOfMessageReadyToSend = messages;
                connectEventArgs.UserToken = theConnectingToken;                
            }
            else
            {
                ConnectOpUserToken theConnectingToken = (ConnectOpUserToken)connectEventArgs.UserToken;
                theConnectingToken.ArrayOfMessageReadyToSend = messages;
            }            

            StartConnect(connectEventArgs, serverEndPoint);

            return SendStatus.OK_ON_OPENNEW;
        }   

        private bool ReuseHeartBeatSAEA(List<Message> messages,string serverEndPointKey) {
            
            try {

                if (!dictPoolOfHeartBeatRecSendEventArgs.ContainsKey(serverEndPointKey))
                {                    
                    return false;
                }

                SocketAsyncEventArgPool thisPortSocketPool = dictPoolOfHeartBeatRecSendEventArgs[serverEndPointKey];
                                
                //while (!thisPortSocketPool.IsEmpty)                
                while (true) 
                {
                    cleanSignal.Reset();

                    //鉴于IsEmpty有问题，确保有Pop尝试!
                    SocketAsyncEventArgs arg = thisPortSocketPool.Pop();

                    if (arg == null)
                    {
                        if (thisPortSocketPool.IsEmpty)                        
                        {                            
                            LogManager.LogTraceInfo(string.Format("poolKey:{0} 's pool empty now with count:{1}", serverEndPointKey, thisPortSocketPool.Count));
                            return false;
                        }
                        else
                        {
                            Thread.Sleep(1);                            
                            continue;
                        }
                    }
                    
                    GetSimplePerf().PerfClientReuseConnectionCounter.Increment();
                    GetSimplePerf().PerfClientIdleConnectionCounter.Decrement();                    

                    ClientUserToken userToken = (ClientUserToken)arg.UserToken;
                    userToken.CreateNewSendDataHolder();
                    SendDataHolder sendDataHodler = userToken.sendDataHolder;

                    sendDataHodler.SetSendMessage(messages);
                    MessagePreparer.GetDataToSend(arg);
                    sendDataHodler.ArrayOfMessageToSend[0].StartTime = DateTime.Now;

                    StartSend(arg);

                    return true;
                }               
            }
            catch(Exception pickErr) {

                LogManager.Log(string.Format("pick socket from poolKey:{0} occur error!", serverEndPointKey), pickErr);
            }
            finally {               

                cleanSignal.Set();
            }

            return false;
        }

        internal void IO_Completed(object sender, SocketAsyncEventArgs e)
        {            
            // determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:

                    ProcessConnect(e);
                    break;

                case SocketAsyncOperation.Receive:

                    ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:

                    ProcessSend(e);
                    break;

                case SocketAsyncOperation.Disconnect:

                    ProcessDisconnectAndCloseSocket(e);
                    break;

                default:
                    {
                        ClientUserToken receiveSendToken = (ClientUserToken)e.UserToken;
                        if (ReceiveFeedbackDataComplete != null)
                        {
                            ReceiveFeedbackDataComplete(receiveSendToken.messageTokenId, null);
                        }

                        this.maxConcurrentConnection.Release();   
                     
                        LogManager.Log(string.Empty, new ArgumentException("\r\nFeedbackError:Error in I/O Completed,返回数据主动设置为NULL, LastOperation: " + e.LastOperation));
                    }
                    break;
            }            
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {            
            ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;
            
            if (receiveSendEventArgs.SocketError != SocketError.Success)
            {
                LogManager.Log(string.Format("SocketError:{0} on {1}",receiveSendEventArgs.SocketError,receiveSendEventArgs.AcceptSocket.RemoteEndPoint), new ArgumentException("SocketError"));
                receiveSendToken.Reset();
                StartDisconnect(receiveSendEventArgs);
                return;
            }
            
            if (receiveSendEventArgs.BytesTransferred == 0)
            {
                LogManager.Log(string.Format("SocketError:{0} on {1} for byte transfer equal zero", receiveSendEventArgs.SocketError, receiveSendEventArgs.AcceptSocket.RemoteEndPoint), new ArgumentException("BytesTransferred"));
                receiveSendToken.Reset();
                StartDisconnect(receiveSendEventArgs);
                return;
            }

            int remainingBytesToProcess = receiveSendEventArgs.BytesTransferred;


            // If we have not got all of the prefix then we need to work on it. 
            // receivedPrefixBytesDoneCount tells us how many prefix bytes were
            // processed during previous receive ops which contained data for 
            // this message. (In normal use, usually there will NOT have been any 
            // previous receive ops here. So receivedPrefixBytesDoneCount would be 0.)
            if (receiveSendToken.receivedPrefixBytesDoneCount < this.prefixHandleLength)
            {
                bool getLengthInfoSuccessfully = false;

                remainingBytesToProcess = PrefixHandler.HandlePrefix(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess,ref getLengthInfoSuccessfully);

                if (remainingBytesToProcess == 0)
                {
                    // We need to do another receive op, since we do not have
                    // the message yet.
                    StartReceive(receiveSendEventArgs);

                    //Jump out of the method, since there is no more data.
                    return;
                }
            }

            // If we have processed the prefix, we can work on the message now.
            // We'll arrive here when we have received enough bytes to read
            // the first byte after the prefix.
            bool incomingTcpMessageIsReady = MessageHandler.HandleMessage(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess);

            if (incomingTcpMessageIsReady == true)
            {               

                //将数据写入缓存字典中
                if (ReceiveFeedbackDataComplete != null)
                {
                    ReceiveFeedbackDataCompleteEventArg arg = new ReceiveFeedbackDataCompleteEventArg();
                    arg.MessageTokenId = receiveSendToken.messageTokenId;
                    arg.FeedbackData = receiveSendToken.dataMessageReceived;

                    if (arg.FeedbackData == null)
                        LogManager.Log("服务端返回数据为NULL!", new ArgumentException(string.Format("FeedbackError:arg.FeedbackData on {0}", arg.MessageTokenId)));

                    ReceiveFeedbackDataComplete(this, arg);
                }
               
                receiveSendToken.Reset();

                //receiveSendToken.sendDataHolder.NumberOfMessageHadSent在ProcessSend成功后修正加一
                //这里是等待回传数据后，开始下次发送前的判断，直至将List<Message>全部发送完毕为止，
                //List<Message>中的数量由调用方控制
                if (receiveSendToken.sendDataHolder.ArrayOfMessageToSend.Count > receiveSendToken.sendDataHolder.NumberOfMessageHadSent)
                {                    
                    MessagePreparer.GetDataToSend(receiveSendEventArgs);
                    StartSend(receiveSendEventArgs);
                }
                else
                {
                    if (!supportKeepAlive)
                    {
                        receiveSendToken.sendDataHolder.ArrayOfMessageToSend = null;
                        //disconnect中会重新new一个senddataholder，numberofmessagehadsent即清零
                        StartDisconnect(receiveSendEventArgs);
                    }
                    else { 

                        //加入support keepalive机制处理逻辑   
                        SetToHeartBeatStatus(receiveSendEventArgs,receiveSendToken);                        
                    }                    
                }               
            }
            else
            {                
                receiveSendToken.receiveMessageOffset = receiveSendToken.bufferOffsetReceive;                
                receiveSendToken.recPrefixBytesDoneThisOp = 0;

                StartReceive(receiveSendEventArgs);
            }
        }

        private void SetToHeartBeatStatus(SocketAsyncEventArgs e,ClientUserToken userToken) {

            //加入support keepalive机制处理逻辑    
            userToken.startTime = DateTime.Now;
            if (!userToken.isReuseConnection)
                userToken.isReuseConnection = true;
            else
            {
                GetSimplePerf().PerfClientReuseConnectionCounter.Decrement();                
            }

            GetSimplePerf().PerfClientIdleConnectionCounter.Increment();

            if (!dictPoolOfHeartBeatRecSendEventArgs.ContainsKey(userToken.ServerEndPointKey))
            {
                lock (this)
                {
                    if (!dictPoolOfHeartBeatRecSendEventArgs.ContainsKey(userToken.ServerEndPointKey))
                    {
                        SocketAsyncEventArgPool pool = new SocketAsyncEventArgPool();
                        pool.Push(e);
                        dictPoolOfHeartBeatRecSendEventArgs.Add(userToken.ServerEndPointKey, pool);
                    }
                    else
                        dictPoolOfHeartBeatRecSendEventArgs[userToken.ServerEndPointKey].Push(e);
                }
            }
            else
                dictPoolOfHeartBeatRecSendEventArgs[userToken.ServerEndPointKey].Push(e);
            
        }

        private void RunHeartBeat() {

            int waitTime = 180000;  
            bool alreadyChecked = false;

            while (true) {

                if (alreadyChecked) {

                    if (!DateTime.Now.Hour.Equals(3)) {

                        alreadyChecked = false;
                    }

                    Thread.Sleep(waitTime);
                }

                if (DateTime.Now.Equals(3)) {

                    foreach (string poolKey in dictPoolOfHeartBeatRecSendEventArgs.Keys)
                    {

                        SocketAsyncEventArgPool thisPortKey = dictPoolOfHeartBeatRecSendEventArgs[poolKey];

                        if (thisPortKey.IsEmpty)
                        {
                            continue;
                        }

                        Stopwatch sw = new Stopwatch();

                        sw.Start();
                        bool existNeedReuseItem = false;

                        cleanSignal.WaitOne();

                        SocketAsyncEventArgs heartBeatSAEA = thisPortKey.Pop();
                        List<SocketAsyncEventArgs> listRepush = new List<SocketAsyncEventArgs>();

                        while (!thisPortKey.IsEmpty)
                        {
                            if (heartBeatSAEA != null)
                            {
                                ClientUserToken userToken = (ClientUserToken)heartBeatSAEA.UserToken;

                                if (DateTime.Now.Subtract(userToken.startTime).TotalSeconds < 120)
                                {
                                    listRepush.Add(heartBeatSAEA);
                                    existNeedReuseItem = true;
                                }
                                else
                                {
                                    //说明太闲了(完全空闲两分钟了!一直没被Pop出去复用),不用发所谓心跳，直接关闭
                                    StartDisconnect(heartBeatSAEA);
                                    GetSimplePerf().PerfClientIdleConnectionCounter.Decrement();
                                }
                            }

                            if (existNeedReuseItem)
                            {
                                //别因为等待信号，导致可复用连接长时间无效闲置
                                if (sw.ElapsedMilliseconds > 100)
                                    break;
                            }

                            cleanSignal.WaitOne();
                            heartBeatSAEA = thisPortKey.Pop();
                        }

                        for (int i = 0; i < listRepush.Count; i++)
                            thisPortKey.Push(listRepush[i]);

                        //if (listRepush.Count > 1)
                        //    thisPortKey.BatchPush(listRepush.ToArray());
                        //else if (listRepush.Count.Equals(1))
                        //    thisPortKey.Push(listRepush[0]);
                    }

                    alreadyChecked = true;

                }                
                
                Thread.Sleep(waitTime);
            }
        }      

        private void StartReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {            
            ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;
            
            receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetReceive, this.bufferSize);

            bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.ReceiveAsync(receiveSendEventArgs);
            if (!willRaiseEvent)
            {
                ProcessReceive(receiveSendEventArgs);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs receiveSendEventArgs)
        {            

            ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;

            if (receiveSendEventArgs.SocketError == SocketError.Success)
            {
                receiveSendToken.sendBytesRemainingCount = receiveSendToken.sendBytesRemainingCount - receiveSendEventArgs.BytesTransferred;
                // If this if statement is true, then we have sent all of the
                // bytes in the message. Otherwise, at least one more send
                // operation will be required to send the data.
                if (receiveSendToken.sendBytesRemainingCount == 0)
                {
                    //incrementing count of messages sent on this connection                
                    receiveSendToken.sendDataHolder.NumberOfMessageHadSent++;

                    //准备接受返回数据
                    StartReceive(receiveSendEventArgs);                                
                }
                else
                {
                    // So since (receiveSendToken.sendBytesRemaining == 0) is false,
                    // we have more bytes to send for this message. So we need to 
                    // call StartSend, so we can post another send message.
                    receiveSendToken.bytesSentAlreadyCount += receiveSendEventArgs.BytesTransferred;
                    StartSend(receiveSendEventArgs);
                }
            }
            else
            {
                string StatusErrorInfo = receiveSendEventArgs.SocketError.ToString();
                receiveSendToken.Reset();
                StartDisconnect(receiveSendEventArgs);

                ReceiveFeedbackDataCompleteEventArg arg = new ReceiveFeedbackDataCompleteEventArg();
                arg.MessageTokenId = receiveSendToken.messageTokenId;
                arg.FeedbackData = null;

                LogManager.Log("发送数据到服务端失败!", new Exception(string.Format("FeedbackError:messageTokenId:{0}因{1}而主动设置为NULL", arg.MessageTokenId, StatusErrorInfo)));

                if (ReceiveFeedbackDataComplete != null)
                {
                    ReceiveFeedbackDataComplete(this, arg);
                }                                                    
            }
        }   

        private void StartSend(SocketAsyncEventArgs receiveSendEventArgs)
        {            
            ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;

            if (receiveSendToken.sendBytesRemainingCount <= this.bufferSize)
            {
                receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
                //Copy the bytes to the buffer associated with this SAEA object.
                Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
            }
            else
            {
                //We cannot try to set the buffer any larger than its size.
                //So since receiveSendToken.sendBytesRemaining > its size, we just
                //set it to the maximum size, to send the most data possible.
                receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, this.bufferSize);
                //Copy the bytes to the buffer associated with this SAEA object.
                Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, this.bufferSize);

                //We'll change the value of sendUserToken.sendBytesRemaining
                //in the ProcessSend method.
            }

            //post the send
            bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.SendAsync(receiveSendEventArgs);
            if (!willRaiseEvent)
            {
                ProcessSend(receiveSendEventArgs);
            }
        }

        private void ProcessConnect(SocketAsyncEventArgs connectEventArgs)
        {            
            ConnectOpUserToken theConnectingToken = (ConnectOpUserToken)connectEventArgs.UserToken;
            
            if (connectEventArgs.SocketError == SocketError.Success)
            {                
                SocketAsyncEventArgs receiveSendEventArgs = poolOfRecSendEventArgs.Pop();
                
                if (receiveSendEventArgs == null)
                {
                    //几乎不可能发生!
                    LogManager.Log(string.Empty, new Exception("fetch receiveSendEventArgs failed for connect"));
                    return;
                }

                Interlocked.Increment(ref currentConnectionCount);
                GetSimplePerf().PerfConcurrentClientConnectionCounter.Increment();                

                receiveSendEventArgs.AcceptSocket = connectEventArgs.AcceptSocket;                

                ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;

                receiveSendToken.sendDataHolder.SetSendMessage(theConnectingToken.ArrayOfMessageReadyToSend);
                receiveSendToken.ServerEndPointKey = theConnectingToken.ServerEndPointKey;

                MessagePreparer.GetDataToSend(receiveSendEventArgs);

                receiveSendToken.startTime = theConnectingToken.ArrayOfMessageReadyToSend[0].StartTime;
                               
                StartSend(receiveSendEventArgs);               

                //release connectEventArgs object back to the pool.
                connectEventArgs.AcceptSocket = null;
                this.poolOfConnectEventArgs.Push(connectEventArgs);
                
            }
            else
            {                
                ProcessConnectionError(connectEventArgs);
            }
        }
        
        private void CloseSocket(Socket theSocket)
        {
            try
            {
                theSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            theSocket.Close();
        }

        private void ProcessConnectionError(SocketAsyncEventArgs connectEventArgs)
        {            
            try
            {
                LogManager.Log(string.Format("ConnectError:{0} for {1}", connectEventArgs.SocketError, connectEventArgs.AcceptSocket != null ? connectEventArgs.AcceptSocket.RemoteEndPoint != null ? connectEventArgs.AcceptSocket.RemoteEndPoint.ToString() : "unknown remote address！" : "unknown remote address"), new Exception("Connect Error!"));

                ConnectOpUserToken theConnectingToken = (ConnectOpUserToken)connectEventArgs.UserToken;               

                // If connection was refused by server or timed out or not reachable, then we'll keep this socket.
                // If not, then we'll destroy it.
                if ((connectEventArgs.SocketError != SocketError.ConnectionRefused) && (connectEventArgs.SocketError != SocketError.TimedOut) && (connectEventArgs.SocketError != SocketError.HostUnreachable))
                {
                    CloseSocket(connectEventArgs.AcceptSocket);
                }               
                
                //返回null数据
                if (ReceiveFeedbackDataComplete != null)
                {                                                                   
                    for (int i = 0; i < theConnectingToken.ArrayOfMessageReadyToSend.Count; i++)                        
                    {                            
                        ReceiveFeedbackDataCompleteEventArg arg = new ReceiveFeedbackDataCompleteEventArg();                            
                        arg.MessageTokenId = theConnectingToken.ArrayOfMessageReadyToSend[i].TokenId;                            
                        arg.FeedbackData = null;

                        LogManager.Log("ConnectionErr!", new Exception(string.Format("FeedbackError:messageTokenId:{0} 因连接错误主动关闭并返回NULL数据", arg.MessageTokenId)));
                            
                        ReceiveFeedbackDataComplete(this, arg);                                                 
                    }
                }

                //it is time to release connectEventArgs object back to the pool.
                poolOfConnectEventArgs.Push(connectEventArgs);

            }
            catch (Exception closeErr)
            {
                LogManager.Log(string.Empty, closeErr);
            }
            finally
            {
                this.maxConcurrentConnection.Release();                
            }
        }

        private void StartConnect(SocketAsyncEventArgs connectEventArgs, IPEndPoint serverEndPoint)
        {
            try {
                
                ConnectOpUserToken theConnectingToken = (ConnectOpUserToken)connectEventArgs.UserToken;
                theConnectingToken.ServerEndPointKey = string.Format("{0}_{1}", serverEndPoint.Address,serverEndPoint.Port);

                connectEventArgs.RemoteEndPoint = serverEndPoint;
                connectEventArgs.AcceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);                            

                bool willRaiseEvent = connectEventArgs.AcceptSocket.ConnectAsync(connectEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessConnect(connectEventArgs);
                }                
            }
            catch (Exception startConnectErr) {

                this.maxConcurrentConnection.Release();
                LogManager.Log("StartConnect", startConnectErr);                
            }            
        }

        private void StartDisconnect(SocketAsyncEventArgs receiveSendEventArgs)
        {                     
            try {                
                receiveSendEventArgs.AcceptSocket.Shutdown(SocketShutdown.Both);               
            }
            catch { }            

            bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.DisconnectAsync(receiveSendEventArgs);
            if (!willRaiseEvent)
            {
                ProcessDisconnectAndCloseSocket(receiveSendEventArgs);
            }
        }

        private void ProcessDisconnectAndCloseSocket(SocketAsyncEventArgs receiveSendEventArgs)
        {            
            try
            {               
                ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;

                //This method closes the socket and releases all resources, both
                //managed and unmanaged. It internally calls Dispose.
                receiveSendEventArgs.AcceptSocket.Close();

                //create an object that we can write data to.
                receiveSendToken.CreateNewSendDataHolder();

                receiveSendToken.isReuseConnection = false;

                // It is time to release this SAEA object.
                this.poolOfRecSendEventArgs.Push(receiveSendEventArgs);                
            }
            catch (Exception shutdownErr)
            {
                LogManager.Log(string.Empty, shutdownErr);
            }
            finally
            {
                this.maxConcurrentConnection.Release();
                Interlocked.Decrement(ref currentConnectionCount);
                GetSimplePerf().PerfConcurrentClientConnectionCounter.Decrement();
            }
        }

    }
}
