using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class SocketAsyncEventArgPool
    {
        private Stack<SocketAsyncEventArgs> pool;

        internal SocketAsyncEventArgPool(Int32 capacity)
		{
			this.pool = new Stack<SocketAsyncEventArgs>(capacity);
		}

        internal int Count
        {
            get { return this.pool.Count; }
        }

        internal SocketAsyncEventArgs Pop()
        {
            lock (this.pool)
            {
                if (this.pool.Count > 0)
                {
                    return this.pool.Pop();
                }
                else
                    return null;
            }
        }

        internal void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            }

            lock (this.pool)
            {
                this.pool.Push(item);
            }
        }
    }
}
