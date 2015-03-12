using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using TcpFramework.Common;

namespace TcpFramework.Server
{
    public class SocketListener
    {       
        private int receivePrefixLength = 0;
        private int bufferSize = 0;
        private Func<byte[], byte[]> dataProcessor;

        //允许的最大并发负责数据传输的socket连接数
        private Semaphore maxConcurrentConnection;

        private bool supportKeepAlive = false;        
        private bool checkLongTimeIdleSAEA = false;
        private ConcurrentDictionary<int, SocketAsyncEventArgs> dictOnlineIdlebyHeartBeatSAEA = new ConcurrentDictionary<int, SocketAsyncEventArgs>();
        
        private Thread thCheckHeartBeatSAEA;
        private SimplePerformanceCounter simplePerf;

        
        private BufferManager bufferManager;

        Socket listenSocket = null;

        SocketAsyncEventArgPool poolOfAcceptEventArgs;
        SocketAsyncEventArgPool poolOfRecSendEventArgs;

        public SocketListener(Func<byte[], byte[]> dataProcessor) {
            
            this.dataProcessor = dataProcessor;

            ServerSetting setting = ReadConfigFile.GetServerSetting();

            this.receivePrefixLength = setting.receivePrefixLength;
            this.bufferSize = setting.bufferSize;
            this.supportKeepAlive = setting.useKeepAlive;

            this.bufferManager = new BufferManager(setting.bufferSize * setting.numberOfSaeaForRecSend * setting.opsToPreAllocate, setting.bufferSize * setting.opsToPreAllocate);            
            this.poolOfAcceptEventArgs = new SocketAsyncEventArgPool();
            this.poolOfRecSendEventArgs = new SocketAsyncEventArgPool();
            this.maxConcurrentConnection = new Semaphore(setting.numberOfSaeaForRecSend, setting.numberOfSaeaForRecSend);

            Init(setting);
            StartListen(setting);
        }

        private void StartListen(ServerSetting serverSetting)
        {
            listenSocket = new Socket(serverSetting.localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(serverSetting.localEndPoint);
            listenSocket.Listen(1000000);

            StartAccept();
        }

        private void Init(ServerSetting serverSetting)
        {
            this.bufferManager.InitBuffer();

            SocketAsyncEventArgs eventArgObjectForPool;

            for (int i = 0; i < serverSetting.maxSimultaneousConnectOps; i++)
            {
                eventArgObjectForPool = new SocketAsyncEventArgs();

                eventArgObjectForPool.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);

                this.poolOfAcceptEventArgs.Push(eventArgObjectForPool);
            }                        

            for (int i = 0; i < serverSetting.numberOfSaeaForRecSend; i++)
            {
                eventArgObjectForPool = new SocketAsyncEventArgs();

                this.bufferManager.SetBuffer(eventArgObjectForPool);
                
                eventArgObjectForPool.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

                ServerUserToken userToken = new ServerUserToken(eventArgObjectForPool.Offset, eventArgObjectForPool.Offset + serverSetting.bufferSize, serverSetting.receivePrefixLength, serverSetting.sendPrefixLength);

                eventArgObjectForPool.UserToken = userToken;

                this.poolOfRecSendEventArgs.Push(eventArgObjectForPool);
            }

            simplePerf = new SimplePerformanceCounter(true);

            if (supportKeepAlive) {

                Console.WriteLine("support keepalive!");
                thCheckHeartBeatSAEA = new Thread(new ThreadStart(RunCheckLongTimeIdleSAEA));
                thCheckHeartBeatSAEA.IsBackground = true;
                thCheckHeartBeatSAEA.Start();                
            }

        }

        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            if (acceptEventArgs.SocketError != SocketError.Success)
            {
                LoopToStartAccept();
               
                LogManager.Log(string.Format("SocketError:{0}", acceptEventArgs.SocketError));

                HandleBadAccept(acceptEventArgs);

                return;
            }            

            simplePerf.PerfConcurrentServerConnectionCounter.Increment();
           
            LoopToStartAccept();

            SocketAsyncEventArgs receiveSendEventArgs = this.poolOfRecSendEventArgs.Pop();

            ServerUserToken userToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            userToken.CreateNewSessionId();

            receiveSendEventArgs.AcceptSocket = acceptEventArgs.AcceptSocket;

            userToken.CreateNewServerSession(receiveSendEventArgs);

            acceptEventArgs.AcceptSocket = null;
            this.poolOfAcceptEventArgs.Push(acceptEventArgs);                                 

            StartReceive(receiveSendEventArgs);
        }

        private void StartAccept()
        {
            this.maxConcurrentConnection.WaitOne();

            SocketAsyncEventArgs acceptEventArg;

            if (this.poolOfAcceptEventArgs.Count > 1)
            {
                try
                {
                    acceptEventArg = this.poolOfAcceptEventArgs.Pop();
                }
                catch
                {
                    acceptEventArg = new SocketAsyncEventArgs();
                    acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
                }
            }
            else
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }                       

            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {               
                ProcessAccept(acceptEventArg);
            }
        }

        private void LoopToStartAccept()
        {           
            StartAccept();
        }

        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {           
            ProcessAccept(e);
        }

        private void HandleBadAccept(SocketAsyncEventArgs acceptEventArgs)
        {           
            acceptEventArgs.AcceptSocket.Close();
            acceptEventArgs.AcceptSocket = null;//win2003 issue,防止safe handle has been closed错误
            this.poolOfAcceptEventArgs.Push(acceptEventArgs);
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {            
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:

                    ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:

                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {
            ServerUserToken receiveSendToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            if (receiveSendEventArgs.SocketError != SocketError.Success)
            {
                LogManager.Log(string.Format("ProcessReceive ERROR: {0}, receiveSendToken sessionid:{1}", receiveSendEventArgs.SocketError, receiveSendToken.serverSession.SessionId));
                
                receiveSendToken.Reset();
                CloseClientSocket(receiveSendEventArgs);

                return;
            }           

            if (receiveSendEventArgs.BytesTransferred == 0)
            {                
                receiveSendToken.Reset();
                CloseClientSocket(receiveSendEventArgs);

                return;
            }
            
            int remainingBytesToProcess = receiveSendEventArgs.BytesTransferred;

            //LogManager.Log(string.Format("getlength:{0} on {1} with prefixDoneCount:{2}", remainingBytesToProcess, receiveSendEventArgs.AcceptSocket.RemoteEndPoint,receiveSendToken.receivedPrefixBytesDoneCount));
            
            if (receiveSendToken.receivedPrefixBytesDoneCount < this.receivePrefixLength)
            {                
                bool getLengthInfoSuccessfully = false;

                remainingBytesToProcess = PrefixHandler.HandlePrefix(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess,ref getLengthInfoSuccessfully);

                //add by saosin on  20150311
                if (getLengthInfoSuccessfully && supportKeepAlive) {
                    
                    if (receiveSendToken.lengthOfCurrentIncomingMessage.Equals(0)) {

                        SetToHeartBeatStatus(receiveSendEventArgs, receiveSendToken);
                        return;
                    }
                }

                if (remainingBytesToProcess == 0)
                {
                    // We need to do another receive op, since we do not have
                    // the message yet, but remainingBytesToProcess == 0.                    
                    StartReceive(receiveSendEventArgs);

                    return;
                }
            }

            bool incomingTcpMessageIsReady = MessageHandler.HandleMessage(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess);
            if (incomingTcpMessageIsReady == true)
            {                
                if (receiveSendToken.dataMessageReceived != null && receiveSendToken.dataMessageReceived.Length > 0)
                {
                    byte[] processResult = this.dataProcessor(receiveSendToken.dataMessageReceived);

                    MessagePreparer.GetDataToSend(receiveSendEventArgs, processResult);

                    //开始下次接收
                    receiveSendToken.CreateNewServerSession(receiveSendEventArgs);
                    receiveSendToken.dataMessageReceived = null;
                    receiveSendToken.Reset();

                    //调整了Reset位置到此语句前面执行，这样确保一个连接多次发送不会出问题了
                    StartSend(receiveSendEventArgs);                  
                }
                else
                {
                    if (!supportKeepAlive)
                    {
                        receiveSendToken.Reset();
                        CloseClientSocket(receiveSendEventArgs);
                        return;
                    }
                    else {

                        //just heartbeat for keepAlive
                        //开始下次接收，不过理论上不该进入该逻辑，毕竟先前判断了内容是否为空！
                        SetToHeartBeatStatus(receiveSendEventArgs, receiveSendToken);
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

        private void StartReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {
            ServerUserToken receiveSendToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetReceive, this.bufferSize);

            bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.ReceiveAsync(receiveSendEventArgs);
            if (!willRaiseEvent)
            {
                ProcessReceive(receiveSendEventArgs);
            }            
        }

        private void StartSend(SocketAsyncEventArgs receiveSendEventArgs)
        {
            ServerUserToken receiveSendToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            if (receiveSendToken.sendBytesRemainingCount <= this.bufferSize)
            {
                receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
                Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
            }
            else
            {
                receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, this.bufferSize);
                Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, this.bufferSize);

                //We'll change the value of sendUserToken.sendBytesRemainingCount
                //in the ProcessSend method.
            }

            //post asynchronous send operation
            bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.SendAsync(receiveSendEventArgs);

            if (!willRaiseEvent)
            {
                ProcessSend(receiveSendEventArgs);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs receiveSendEventArgs)
        {
            ServerUserToken receiveSendToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            if (receiveSendEventArgs.SocketError == SocketError.Success)
            {
                receiveSendToken.sendBytesRemainingCount = receiveSendToken.sendBytesRemainingCount - receiveSendEventArgs.BytesTransferred;

                if (receiveSendToken.sendBytesRemainingCount == 0)
                {
                    // If we are within this if-statement, then all the bytes in
                    // the message have been sent.                     
                    StartReceive(receiveSendEventArgs);
                }
                else
                {
                    receiveSendToken.bytesSentAlreadyCount += receiveSendEventArgs.BytesTransferred;
                    // So let's loop back to StartSend().
                    StartSend(receiveSendEventArgs);
                }
            }
            else
            {
                receiveSendToken.Reset();
                CloseClientSocket(receiveSendEventArgs);
            }
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            var receiveSendToken = (e.UserToken as ServerUserToken);

            try
            {
                e.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            //This method closes the socket and releases all resources, both
            //managed and unmanaged. It internally calls Dispose.
            e.AcceptSocket.Close();

            //Make sure the new DataHolder has been created for the next connection.
            //If it has, then dataMessageReceived should be null.
            receiveSendToken.dataMessageReceived = null;

            if (supportKeepAlive) {

                if (!checkLongTimeIdleSAEA) { 
                    
                    if(dictOnlineIdlebyHeartBeatSAEA.ContainsKey(receiveSendToken.SessionId)){

                        //Socket异常时导致的关闭，才会进入这个处理
                        SocketAsyncEventArgs arg;
                        dictOnlineIdlebyHeartBeatSAEA.TryRemove(receiveSendToken.SessionId, out arg);                         
                    }
                }
            }

            // Put the SocketAsyncEventArg back into the pool,
            // to be used by another client. This 
            this.poolOfRecSendEventArgs.Push(e);

            simplePerf.PerfConcurrentServerConnectionCounter.Decrement();

            //Release Semaphore so that its connection counter will be decremented.
            //This must be done AFTER putting the SocketAsyncEventArg back into the pool,
            //or you can run into problems.
            this.maxConcurrentConnection.Release();
        }

        private void SetToHeartBeatStatus(SocketAsyncEventArgs receiveSendEventArgs, ServerUserToken receiveSendToken)
        {
            //just heartbeat for keepAlive，即不主动关闭，只是挂起并记录入字典便于清除长时间挂起SAEA
            //开始下次接收
            receiveSendToken.CreateNewServerSession(receiveSendEventArgs,true);
            receiveSendToken.dataMessageReceived = null;
            receiveSendToken.Reset();

            if (dictOnlineIdlebyHeartBeatSAEA.ContainsKey(receiveSendToken.SessionId))
            {
                //Console.WriteLine(string.Format("heartbeat saea already exist!{0}", receiveSendEventArgs.AcceptSocket.RemoteEndPoint));
                StartReceive(receiveSendEventArgs);
                simplePerf.PerfHeartBeatStatusServerConnectionCounter.Increment();
                return;
            }

            bool result = dictOnlineIdlebyHeartBeatSAEA.TryAdd(receiveSendToken.SessionId, receiveSendEventArgs);

            if (result)
            {
                //Console.WriteLine(string.Format("try add heartbeat saea successfully!{0}", receiveSendEventArgs.AcceptSocket.RemoteEndPoint));
                StartReceive(receiveSendEventArgs);
                simplePerf.PerfHeartBeatStatusServerConnectionCounter.Increment();
            }
            else
            {
                //Console.WriteLine(string.Format("try add heartbeat saea falied then we close it!{0}", receiveSendEventArgs.AcceptSocket.RemoteEndPoint));
                CloseClientSocket(receiveSendEventArgs);
                simplePerf.PerfHeartBeatStatusServerConnectionCounter.Decrement();
            }            
        }

        private List<int> GetShouldRemoveLongTimeIdleOnlineSAEA() {
           
            List<int> listLongTimeIdleSessionId = new List<int>();

            //Console.WriteLine(string.Format("establish online saea count:{0}", dictOnlineIdlebyHeartBeatSAEA.Count));

            foreach (int sessionId in dictOnlineIdlebyHeartBeatSAEA.Keys) {

                SocketAsyncEventArgs arg = dictOnlineIdlebyHeartBeatSAEA[sessionId];

                ServerUserToken userToken = (ServerUserToken)arg.UserToken;

                if (userToken.serverSession.OnHeartBeatStatus) {

                    if (DateTime.Now.Subtract(userToken.serverSession.CreateSessionTime).TotalSeconds > 60)
                    {
                        listLongTimeIdleSessionId.Add(sessionId);                        
                    }
                }
            }            

            return listLongTimeIdleSessionId;
        }

        private void RunCheckLongTimeIdleSAEA() {

            while (true) {

                checkLongTimeIdleSAEA = true;

                List<int> listSid = GetShouldRemoveLongTimeIdleOnlineSAEA();

                //Console.WriteLine(string.Format("need force close socket count:{0}", listSid.Count));

                if (listSid.Count > 0) {

                    for (int i = 0; i < listSid.Count; i++) {

                        SocketAsyncEventArgs arg;
                        bool result = dictOnlineIdlebyHeartBeatSAEA.TryRemove(listSid[i],out arg);

                        if (result) {

                            if (arg.AcceptSocket != null) {

                                try {

                                    //Console.WriteLine(string.Format("force close remote socket:{0}", arg.AcceptSocket.RemoteEndPoint));

                                    ServerUserToken userToken = (ServerUserToken)arg.UserToken;
                                    userToken.Reset();
                                    CloseClientSocket(arg);  
                                }
                                catch { }
                            }                                                     
                        }                        
                    }
                }

                checkLongTimeIdleSAEA = false;

                Thread.Sleep(10000);
            }
        }

        public void Stop()
        {

            try {

                listenSocket.Close();
                listenSocket = null;

                bufferManager = null;
                poolOfAcceptEventArgs = null;
                poolOfRecSendEventArgs = null;
            }
            catch { }            
        }
    }
}
