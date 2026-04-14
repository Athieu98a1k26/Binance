using HedgeBot.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot.BackTest
{
    public class DataTool
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task DownloadHistoryToCsv(string symbol, string interval, DateTime startTime, string filePath)
        {
            long? startMs = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
            List<string> allLines = new List<string>();

            Console.WriteLine($"🚀 Đang tải dữ liệu {symbol} từ {startTime:yyyy-MM-dd}...");

            while (true)
            {
                string url = $"https://fapi.binance.com/fapi/v1/klines?symbol={symbol}&interval={interval}&startTime={startMs}&limit=1000";
                var response = await _httpClient.GetStringAsync(url);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic[][]>(response);

                if (data == null || data.Length == 0) break;

                foreach (var item in data)
                {
                    // Format: openTime, open, high, low, close, volume
                    allLines.Add($"{item[0]},{item[1]},{item[2]},{item[3]},{item[4]},{item[5]}");
                }

                long lastTime = (long)data[^1][0];
                if (lastTime >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60000) break;

                startMs = lastTime + 1;
                Console.WriteLine($"--- Đã tải đến: {DateTimeOffset.FromUnixTimeMilliseconds(lastTime).DateTime:yyyy-MM-dd HH:mm}");
                await Task.Delay(200); // Tránh bị ban IP
            }

            File.WriteAllLines(filePath, allLines);
            Console.WriteLine($"✅ Đã lưu {allLines.Count} nến vào {filePath}");
        }

        public static List<Candle> LoadFromCsv(string filePath)
        {
            return File.ReadAllLines(filePath).Select(line => {
                var c = line.Split(',');
                return new Candle
                {
                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(c[0])).DateTime,
                    Open = decimal.Parse(c[1], CultureInfo.InvariantCulture),
                    High = decimal.Parse(c[2], CultureInfo.InvariantCulture),
                    Low = decimal.Parse(c[3], CultureInfo.InvariantCulture),
                    Close = decimal.Parse(c[4], CultureInfo.InvariantCulture),
                    Volume = decimal.Parse(c[5], CultureInfo.InvariantCulture)
                };
            }).ToList();
        }
    }
}
