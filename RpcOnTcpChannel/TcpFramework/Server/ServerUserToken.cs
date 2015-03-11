using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;

using TcpFramework.Common;

namespace TcpFramework.Server
{
    internal class ServerUserToken : DataUserToken
    {
        private static int ServerSessionId;
        private static int ServerTransmissionId;

        private int sessionId;
        internal ServerSession serverSession;

        public ServerUserToken(int receiveOffset, int sendOffset, int receivePrefixLength, int sendPrefixLength) : base(receiveOffset, sendOffset, receivePrefixLength, sendPrefixLength) { }

        internal void CreateNewSessionId() {

            this.sessionId = Interlocked.Increment(ref ServerSessionId);
        }

        internal void CreateNewServerSession(SocketAsyncEventArgs e,bool onHeartBeatStatus = false) {

            serverSession = new ServerSession();
            serverSession.SessionId = this.sessionId;
            serverSession.ReceiveTransmissionId = Interlocked.Increment(ref ServerTransmissionId);
            serverSession.RemoteEndPoint = e.AcceptSocket.RemoteEndPoint;
            serverSession.CreateSessionTime = DateTime.Now;
            serverSession.OnHeartBeatStatus = onHeartBeatStatus;
        }

        internal int SessionId {

            get { return this.sessionId; }
        }
    }
}
