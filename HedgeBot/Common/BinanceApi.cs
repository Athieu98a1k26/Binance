using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text.Json;
using HedgeBot.Models;

namespace HedgeBot.Common
{
    static class BinanceApi
    {
        private static readonly HttpClient client = new HttpClient();
        private const string BaseUrl = "https://fapi.binance.com";

        public static async Task<decimal> GetCurrentPrice(string symbol)
        {
            var url = $"{BaseUrl}/fapi/v1/ticker/price?symbol={symbol}";
            var json = await client.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            return decimal.Parse(doc.RootElement.GetProperty("price").GetString()!);
        }

        public static async Task<List<BinanceKline>> GetKlines(string symbol, string interval, int limit)
        {
            var json = await client.GetStringAsync($"{BaseUrl}/fapi/v1/klines?symbol={symbol.ToUpper()}&interval={interval}&limit={limit}");

            // 1. Giải nén ra mảng thô trước (mảng của mảng)
            var rawData = JsonConvert.DeserializeObject<List<List<object>>>(json);

            if (rawData == null) return new List<BinanceKline>();

            // 2. Map từ mảng thô sang danh sách Class BinanceKline
            return rawData.Select(x => new BinanceKline
            {
                // x[0] là Open Time, nếu bạn cần quản lý thời gian nến
                // x[1] là Open Price (Bắt buộc phải có để tính Body nến)
                Open = decimal.Parse(x[1].ToString(), System.Globalization.CultureInfo.InvariantCulture),

                High = decimal.Parse(x[2].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                Low = decimal.Parse(x[3].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                Close = decimal.Parse(x[4].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                Volume = decimal.Parse(x[5].ToString(), System.Globalization.CultureInfo.InvariantCulture)
            }).ToList();
        }
    }
}
