using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using IProcessGoodDetail;

namespace TestSharpRpcSample
{
    public class GoodManager : IGoodManager
    {
        public string GetGoodDetail(string[] goodId, long lat, long lng, string cityName) {            

            StringBuilder sbGoodIds = new StringBuilder();

            for (int i = 0; i < goodId.Length; i++) {

                sbGoodIds.AppendFormat("{0},", goodId[i]);
            }

            sbGoodIds.Append(DateTime.Now.Ticks.ToString());

            Thread.Sleep(50);

            return string.Format("ret:ids({0} on city:{1} with coord:[lat:{2},lng{3}] rand:{4})", sbGoodIds.ToString(), cityName, lat, lng, new Random().Next(100000));
        }

        public string[] GetGoodList(string cityName, int category, int subCategory, long lat, long lng) {

            string[] strInfos = new string[new Random().Next(100)];

            for (int i = 0; i < strInfos.Length; i++) {

                string info = string.Format("id:{5} -- city:{0} category:{1} subcategory:{2} lat:{3} lng:{4} ", cityName, category, subCategory, lat, lng);
                strInfos[i] = info;
            }

            return strInfos;
        }
    }
}
