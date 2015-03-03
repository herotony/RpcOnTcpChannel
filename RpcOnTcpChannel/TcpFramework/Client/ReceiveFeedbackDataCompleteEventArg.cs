using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpFramework.Client
{
    public class ReceiveFeedbackDataCompleteEventArg : EventArgs
    {
        public int MessageTokenId { get; set; }
        public byte[] FeedbackData { get; set; }
    }
}
