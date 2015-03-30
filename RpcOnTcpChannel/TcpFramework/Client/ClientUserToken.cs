using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TcpFramework.Common;


namespace TcpFramework.Client
{
    internal class ClientUserToken : DataUserToken
    {
        internal SendDataHolder sendDataHolder;
        internal bool isReuseConnection;

        public ClientUserToken(int receiveOffset, int sendOffset, int receivePrefixLength, int sendPrefixLength) : base(receiveOffset, sendOffset, receivePrefixLength, sendPrefixLength) {

            isReuseConnection = false;
        }

        internal void CreateNewSendDataHolder() {

            this.sendDataHolder = new SendDataHolder();            
        }
    }
}
