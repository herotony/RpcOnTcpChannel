using System;
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
        public int maxConcurrentConnectOpCount = 0;
        public int maxConcurrentRecSendCount = 0;

        public int concurrentConnectOpCount = 0;
        public int concurrentRecSendCount = 0;

        private int receivePrefixLength = 0;
        private int bufferSize = 0;
        private Func<byte[], byte[]> dataProcessor;

        //允许的最大并发负责数据传输的socket连接数
        private Semaphore maxConcurrentConnection;

        private BufferManager bufferManager;

        Socket listenSocket = null;

        SocketAsyncEventArgPool poolOfAcceptEventArgs;
        SocketAsyncEventArgPool poolOfRecSendEventArgs;

        public SocketListener(Func<byte[], byte[]> dataProcessor) {

            this.dataProcessor = dataProcessor;

            ServerSetting setting = ReadConfigFile.GetServerSetting();

            this.receivePrefixLength = setting.receivePrefixLength;
            this.bufferSize = setting.bufferSize;

            this.bufferManager = new BufferManager(setting.bufferSize * setting.numberOfSaeaForRecSend * setting.opsToPreAllocate, setting.bufferSize * setting.opsToPreAllocate);
            this.poolOfAcceptEventArgs = new SocketAsyncEventArgPool(setting.maxSimultaneousConnectOps);
            this.poolOfRecSendEventArgs = new SocketAsyncEventArgPool(setting.numberOfSaeaForRecSend);
            this.maxConcurrentConnection = new Semaphore(setting.numberOfSaeaForRecSend, setting.numberOfSaeaForRecSend);

            Init(setting);
            StartListen(setting);
        }

        private void StartListen(ServerSetting serverSetting)
        {
            listenSocket = new Socket(serverSetting.localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(serverSetting.localEndPoint);
            listenSocket.Listen(100000);

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

            int numberOfConnectedSockets = Interlocked.Increment(ref this.concurrentConnectOpCount);
            if (numberOfConnectedSockets > this.maxConcurrentConnectOpCount)
            {
                this.maxConcurrentConnectOpCount = numberOfConnectedSockets;
            }
           
            LoopToStartAccept();

            SocketAsyncEventArgs receiveSendEventArgs = this.poolOfRecSendEventArgs.Pop();

            ServerUserToken userToken = (ServerUserToken)receiveSendEventArgs.UserToken;

            userToken.CreateNewSessionId();

            receiveSendEventArgs.AcceptSocket = acceptEventArgs.AcceptSocket;

            userToken.CreateNewServerSession(receiveSendEventArgs);

            acceptEventArgs.AcceptSocket = null;
            this.poolOfAcceptEventArgs.Push(acceptEventArgs);

            Interlocked.Decrement(ref this.concurrentConnectOpCount);
            int numberOfRecSendCount = Interlocked.Increment(ref this.concurrentRecSendCount);
            if (numberOfRecSendCount > maxConcurrentRecSendCount)
                this.maxConcurrentRecSendCount = numberOfRecSendCount;                        

            StartReceive(receiveSendEventArgs);
        }

        private void StartAccept()
        {
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

            this.maxConcurrentConnection.WaitOne();

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

            if (receiveSendToken.receivedPrefixBytesDoneCount < this.receivePrefixLength)
            {
                remainingBytesToProcess = PrefixHandler.HandlePrefix(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess);
                
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

                    StartSend(receiveSendEventArgs);

                    receiveSendToken.dataMessageReceived = null;
                    receiveSendToken.Reset();

                }
                else
                {
                    receiveSendToken.Reset();
                    CloseClientSocket(receiveSendEventArgs);
                    return;
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

            LogManager.Log(string.Format("receive on {0} with {1}", receiveSendEventArgs.AcceptSocket.RemoteEndPoint, willRaiseEvent));
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

            // decrement the counter keeping track of the total number of clients 
            //connected to the server, for testing
            Interlocked.Decrement(ref this.concurrentRecSendCount);

            //Release Semaphore so that its connection counter will be decremented.
            //This must be done AFTER putting the SocketAsyncEventArg back into the pool,
            //or you can run into problems.
            this.maxConcurrentConnection.Release();
        }

    }
}
