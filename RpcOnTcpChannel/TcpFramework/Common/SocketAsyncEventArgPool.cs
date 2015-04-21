using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class SocketAsyncEventArgPool
    {
        //private ConcurrentStack<SocketAsyncEventArgs> pool;
        private ConcurrentQueue<SocketAsyncEventArgs> _pool;

        internal SocketAsyncEventArgPool()
		{
			//this.pool = new ConcurrentStack<SocketAsyncEventArgs>();
            this._pool = new ConcurrentQueue<SocketAsyncEventArgs>();
		}

        internal int Count
        {
            //get { return this.pool.Count; }
            get { return this._pool.Count; }
        }

        internal bool IsEmpty {

            //get { return this.pool.IsEmpty; }
            get { return this._pool.IsEmpty; }
        }

        internal SocketAsyncEventArgs Pop()
        {           
            SocketAsyncEventArgs idleSAEA;
            
            //if (!this.pool.TryPop(out idleSAEA))
            //    return null;
            if (!this._pool.TryDequeue(out idleSAEA))
                return null;

            return idleSAEA;
        }

        internal void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");                
            }

            //this.pool.Push(item);           
            this._pool.Enqueue(item);
        }

        internal void BatchPush(SocketAsyncEventArgs[] arrItem) {

            //this.pool.PushRange(arrItem, 0, arrItem.Length);            
        }
    }
}
