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

            LogManager.Log(string.Format("listen on {0}", serverSetting.localEndPoint));

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

            simplePerf = new SimplePerformanceCounter(true,true);           
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
                if(receiveSendEventArgs.SocketError!= SocketError.ConnectionReset)
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
                    //即便processResult为byte[0]，也会返回类似sessionid,transessionid头信息回客户端
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
                               
                        //只是个万全做法，不应该有此逻辑，即传输内容为空!
                        receiveSendToken.CreateNewServerSession(receiveSendEventArgs);
                        receiveSendToken.dataMessageReceived = null;
                        receiveSendToken.Reset();

                        StartReceive(receiveSendEventArgs);
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

            // Put the SocketAsyncEventArg back into the pool,
            // to be used by another client. This 
            this.poolOfRecSendEventArgs.Push(e);

            simplePerf.PerfConcurrentServerConnectionCounter.Decrement();

            //Release Semaphore so that its connection counter will be decremented.
            //This must be done AFTER putting the SocketAsyncEventArg back into the pool,
            //or you can run into problems.
            this.maxConcurrentConnection.Release();
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
