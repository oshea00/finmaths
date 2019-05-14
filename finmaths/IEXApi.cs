using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace finmaths
{
    static class IEXApi
    {
        public static List<IEXPriceHistoryItem> LoadPriceHistoryBlocking(string ticker, string range, string url, string token)
        {
            var client = new WebClient();
            var urlstring = $"{url}/stock/{ticker}/chart/{range}?token={token}";
            var uri = new Uri(urlstring);
            var pricedata = client.DownloadString(uri);
            var prices = new List<IEXPriceHistoryItem>();
            var items = JArray.Parse(pricedata);
            foreach (JObject item in items.Children())
            {
                prices.Add(item.ToObject<IEXPriceHistoryItem>());
            }
            return prices;
        }

        public static async Task<List<IEXPriceHistoryItem>> LoadPriceHistoryTaskAsync(string ticker, string range, string url, string token)
        {
            var client = new WebClient();
            var urlstring = $"{url}/stock/{ticker}/chart/{range}?token={token}";
            var uri = new Uri(urlstring);
            var pricedata = await client.DownloadStringTaskAsync(uri);
            var prices = new List<IEXPriceHistoryItem>();
            var items = JArray.Parse(pricedata);
            foreach (JObject item in items.Children())
            {
                prices.Add(item.ToObject<IEXPriceHistoryItem>());
            }
            return prices;
        }

    }
}
