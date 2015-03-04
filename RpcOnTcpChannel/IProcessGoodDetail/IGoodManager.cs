using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IProcessGoodDetail
{
    public interface IGoodManager
    {
        string GetGoodDetail(string[] goodId, long lat, long lng, string cityName);
        string[] GetGoodList(string cityName, int category, int subCategory, long lat, long lng);
    }
}
