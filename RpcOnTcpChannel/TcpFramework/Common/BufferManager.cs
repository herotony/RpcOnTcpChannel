using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace TcpFramework.Common
{
    internal class BufferManager
    {
        //鉴于socket所使用的buffer不受托管控制，这里统一维护一个大块连续内存区域以提高效率！
        //实现为每个负责具体数据发送和接收的socket在其上分配固定的区域。
        int totalBytesInBufferBlock;        
        byte[] bufferBlock;

        Stack<int> freeIndexPool;
        int currentIndex;
        int bufferBytesAllocatedForEachSaea;
        bool initPhrase = true;

        //totalBytes % totalBufferBytesInEachSaeaObject 必须整除才合理
        public BufferManager(int totalBytes, int totalBufferBytesInEachSaeaObject)
        {
            totalBytesInBufferBlock = totalBytes;
            this.currentIndex = 0;
            this.bufferBytesAllocatedForEachSaea = totalBufferBytesInEachSaeaObject;
            this.freeIndexPool = new Stack<int>();
        }

        //统一分配大块连续的区域
        internal void InitBuffer()
        {            
            this.bufferBlock = new byte[totalBytesInBufferBlock];
        }

        internal void SetInitComplete() {

            initPhrase = false;
        }

        //为具体的，正在使用中的socket分配固定区域
        internal bool SetBuffer(SocketAsyncEventArgs args)
        {            
            
            if (this.freeIndexPool.Count > 0)
            {
                lock (this) {

                    if (this.freeIndexPool.Count > 0)
                        args.SetBuffer(this.bufferBlock, this.freeIndexPool.Pop(), this.bufferBytesAllocatedForEachSaea);
                    else
                        return false;
                }
                
            }
            
            if(initPhrase)
            {
                if ((totalBytesInBufferBlock - this.bufferBytesAllocatedForEachSaea) < this.currentIndex)
                {
                    return false;
                }
                args.SetBuffer(this.bufferBlock, this.currentIndex, this.bufferBytesAllocatedForEachSaea);
                this.currentIndex += this.bufferBytesAllocatedForEachSaea;
            }

            return true;
        }

        // on the server
        // keep the same buffer space assigned to one SAEA object for the duration of
        // this app's running.
        //服务端不该调用此方法！
        internal void FreeBuffer(SocketAsyncEventArgs args)
        {
            this.freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
