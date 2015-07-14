using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;
using HttpManager;

namespace NUnitTest
{
    [TestFixture]
    public class Test_HttpManager 
    {
        [Test]
        public void TestEncodeDecodeData() {

            Queue<ClientData> queue = new Queue<ClientData>();
            int totalLength = 0;

            totalLength = DataManager.PushClientDataToQueueAndFeedbackLength(queue, "5");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "sendrequest_1");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "s2endrequest_2");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "s3endrequest3_1");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "sendr4eques4t_1");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "send5r\"11\"e5quest_1");

            byte[] sendData = DataManager.GetSendData(queue, totalLength);

            string[] tt = DataManager.DecodeByteData(sendData);

            Assert.AreEqual(5, tt.Length);
            Assert.AreEqual("sendrequest_1", tt[0]);
            Assert.AreEqual("s2endrequest_2", tt[1]);
            Assert.AreEqual("s3endrequest3_1", tt[2]);
            Assert.AreEqual("sendr4eques4t_1", tt[3]);
            Assert.AreEqual("send5r\"11\"e5quest_1", tt[4]);
        }

        [Test]
        public void TestServerDecodeData() {

            Queue<ClientData> queue = new Queue<ClientData>();
            int totalLength = 0;

            totalLength = DataManager.PushClientDataToQueueAndFeedbackLength(queue, "6");

            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "sendrequest_2");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "payorder");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "136.8.10.10");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, ".netfrmawork;ipad;don'tknow");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "true");
            totalLength += DataManager.PushClientDataToQueueAndFeedbackLength(queue, "<req cmd=\"fint\">仅仅是测试</req>");

            byte[] sendData = DataManager.GetSendData(queue, totalLength);

            string command=string.Empty,ip=string.Empty,ua=string.Empty,reqstr;
            bool isneedencrypt=false;

            reqstr = HttpManager.Server.GetRequestString(sendData, ref command, ref ip, ref ua, ref isneedencrypt);

            Assert.AreEqual("payorder", command);
            Assert.AreEqual("136.8.10.10", ip);
            Assert.AreEqual(".netfrmawork;ipad;don'tknow", ua);
            Assert.AreEqual(true, isneedencrypt);
            Assert.AreEqual("<req cmd=\"fint\">仅仅是测试</req>", reqstr);
        }
    }
}
