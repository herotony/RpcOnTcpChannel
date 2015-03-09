using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class SocketAsyncEventArgPool
    {
        private ConcurrentStack<SocketAsyncEventArgs> pool;

        internal SocketAsyncEventArgPool()
		{
			this.pool = new ConcurrentStack<SocketAsyncEventArgs>();
		}

        internal int Count
        {
            get { return this.pool.Count; }
        }

        internal SocketAsyncEventArgs Pop()
        {
            SocketAsyncEventArgs idleSAEA;

            if (!this.pool.TryPop(out idleSAEA))
                return null;

            return idleSAEA;
        }

        internal void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            }


            this.pool.Push(item);           
        }
    }
}
