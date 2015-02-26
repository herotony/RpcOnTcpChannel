using System;

namespace TcpFramework.Client
{
    public class Message
    {
        public DateTime StartTime { get; set; }
        public int TokenId { get; set; }
        public byte[] Content { get; set; }
    }
}
