using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

using TcpFramework.Common;

namespace TcpFramework.Client
{
    internal class ClientSocketProcessor
    {        
        public event EventHandler<ReceiveFeedbackDataCompleteEventArg> ReceiveFeedbackDataComplete;
        
        #region 统计数据

        //运行以来的最大并发值
        internal int maxConnectOpCountEver = 0;
        internal int maxConnectRecSendCountEver = 0;

        //当前并发值
        internal int currentConnectOpCount = 0;
        internal int currentRecSendCount = 0;

        #endregion

        private SocketAsyncEventArgPool poolOfRecSendEventArgs;
        private SocketAsyncEventArgPool poolOfConnectEventArgs;

        //允许的最大并发连接数
        private Semaphore maxConcurrentConnection;

        //每个连接最多能发送的消息数量，一般为一次一条
        private int numberMessagesOfPerConnection;
        //一般为整型int的四个字节
        private int prefixHandleLength;
        private int bufferSize;

        public ClientSocketProcessor(SocketAsyncEventArgPool connectPool, SocketAsyncEventArgPool recSendPool, int maxRecSendConnection, int bufferSize, int numberMessageOfPerConnection, int prefixHandleLength) {

            this.poolOfConnectEventArgs = connectPool;
            this.poolOfRecSendEventArgs = recSendPool;

            this.maxConcurrentConnection = new Semaphore(maxRecSendConnection, maxRecSendConnection);

            this.bufferSize = bufferSize;
            this.numberMessagesOfPerConnection = numberMessageOfPerConnection;
            this.prefixHandleLength = prefixHandleLength;

        }

        internal void SendMessage(List<Message> messages, IPEndPoint serverEndPoint)
        {
            //允许最大的并发传输数量为maxRecSendConnection，否则处于等待！
            maxConcurrentConnection.WaitOne();

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
                        LogManager.Log(string.Empty, new ArgumentException("\r\nError in I/O Completed, LastOperation: " + e.LastOperation));
                    }
                    break;
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveSendEventArgs)
        {
            ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;
            
            if (receiveSendEventArgs.SocketError != SocketError.Success)
            {
                receiveSendToken.Reset();
                StartDisconnect(receiveSendEventArgs);
                return;
            }
            
            if (receiveSendEventArgs.BytesTransferred == 0)
            {
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
                remainingBytesToProcess = PrefixHandler.HandlePrefix(receiveSendEventArgs, receiveSendToken, remainingBytesToProcess);

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
                    receiveSendToken.sendDataHolder.ArrayOfMessageToSend = null;
                    //disconnect中会重新new一个senddataholder，numberofmessagehadsent即清零
                    StartDisconnect(receiveSendEventArgs);
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
                receiveSendToken.Reset();
                StartDisconnect(receiveSendEventArgs);

                // We'll just close the socket if there was a
                // socket error when receiving data from the client.
                if (ReceiveFeedbackDataComplete != null)
                {
                    ReceiveFeedbackDataCompleteEventArg arg = new ReceiveFeedbackDataCompleteEventArg();
                    arg.MessageTokenId = receiveSendToken.messageTokenId;
                    arg.FeedbackData = null;

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

            Interlocked.Decrement(ref currentConnectOpCount);

            if (connectEventArgs.SocketError == SocketError.Success)
            {
                SocketAsyncEventArgs receiveSendEventArgs = poolOfRecSendEventArgs.Pop();
                
                if (receiveSendEventArgs == null)
                {
                    //几乎不可能发生!
                    LogManager.Log(string.Empty, new Exception("fetch receiveSendEventArgs failed for connect"));
                    return;
                }

                receiveSendEventArgs.AcceptSocket = connectEventArgs.AcceptSocket;

                ClientUserToken receiveSendToken = (ClientUserToken)receiveSendEventArgs.UserToken;

                receiveSendToken.sendDataHolder.SetSendMessage(theConnectingToken.ArrayOfMessageReadyToSend);

                MessagePreparer.GetDataToSend(receiveSendEventArgs);

                receiveSendToken.startTime = theConnectingToken.ArrayOfMessageReadyToSend[0].StartTime;
                               
                StartSend(receiveSendEventArgs);

                Interlocked.Increment(ref currentRecSendCount);
                if (currentRecSendCount > maxConnectRecSendCountEver)
                    maxConnectRecSendCountEver = currentRecSendCount;

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
            ConnectOpUserToken theConnectingToken = (ConnectOpUserToken)connectEventArgs.UserToken;

            connectEventArgs.RemoteEndPoint = serverEndPoint;
            connectEventArgs.AcceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Interlocked.Increment(ref currentConnectOpCount);
            if (currentConnectOpCount > maxConnectOpCountEver)
                maxConnectOpCountEver = currentConnectOpCount;          

            bool willRaiseEvent = connectEventArgs.AcceptSocket.ConnectAsync(connectEventArgs);
            if (!willRaiseEvent)
            {
                ProcessConnect(connectEventArgs);
            }
        }

        private void StartDisconnect(SocketAsyncEventArgs receiveSendEventArgs)
        {
            Interlocked.Decrement(ref currentRecSendCount);

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
            }
        }

    }
}
