using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace TcpFramework.Server
{
    internal class ServerSetting
    {        
        //设置最大并发处理连接的saea
		internal int maxSimultaneousConnectOps{get;private set;}	

		//设置最大并发处理数据传输的saea
		internal int numberOfSaeaForRecSend{get;private set;}

		//每个用于真正数据传输socket的buffersize
		internal int bufferSize{get;private set;}

		//接收消息长度信息所占字节数，一般为4个字节，即int型整数，不会变更。
		internal int receivePrefixLength{get;private set;}

        //发送消息长度信息所占字节数，一般为4个字节，即int型整数，不会变更。
		internal int sendPrefixLength{get;private set;}

		// 1 for receive, 1 for send. used in BufferManager，所以一般为2，代表一个用于收数据，一个用于发数据
		//用于BufferManager时，就是：numberOfSaeaForRecSend * bufferSize * 2 而每个连接的是 buffersize * 2
		internal int opsToPreAllocate{get;private set;}

        internal bool useKeepAlive { get; private set; }

		internal IPEndPoint localEndPoint;


        public ServerSetting(IPEndPoint theLocalEndPoint, int maxSimultaneousConnectOps, int numberOfSaeaForRecSend, int bufferSize = 128, int receivePrefixLength = 4, int sendPrefixLength = 4, int opsToPreAlloc = 2,bool useKeepAlive = true)
		{
            this.localEndPoint = theLocalEndPoint;          

			this.maxSimultaneousConnectOps = maxSimultaneousConnectOps;
            this.numberOfSaeaForRecSend = numberOfSaeaForRecSend;
           
            this.bufferSize = bufferSize;
            this.useKeepAlive = useKeepAlive;

            //下面这些都应该是默认值，永不修改！
			this.receivePrefixLength = receivePrefixLength;			
			this.sendPrefixLength = sendPrefixLength;
			this.opsToPreAllocate = opsToPreAlloc;									
		}
    }
}
