using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net.Clients;
using Binance.Net.Enums;
using MLTrain.Models;

namespace MLTrain.Services
{
    public class DataService
    {
        private BinanceRestClient _client = new();

        public async Task<List<Candle>> GetCandlesAsync(
       string symbol = "BTCUSDT",
       KlineInterval interval = KlineInterval.FiveMinutes,
       int totalCandles = 10000)
        {
            var allCandles = new List<Candle>();
            int batchSize = 1000; // Binance giới hạn 1000/request
            DateTime? endTime = null;

            Console.WriteLine($"[Binance] Đang lấy {totalCandles} nến {symbol} {interval}...");

            while (allCandles.Count < totalCandles)
            {
                int limit = Math.Min(batchSize, totalCandles - allCandles.Count);

                var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(
                    symbol,
                    interval,
                    endTime: endTime,
                    limit: limit
                );

                if (!result.Success || result.Data == null)
                {
                    Console.WriteLine($"[Binance] Lỗi: {result.Error?.Message}");
                    break;
                }

                var klines = result.Data.ToList();
                if (!klines.Any()) break;

                var candles = klines
                    .OrderBy(k => k.OpenTime)
                    .Select(k => new Candle
                    {
                        Time = k.OpenTime,
                        Open = k.OpenPrice,
                        High = k.HighPrice,
                        Low = k.LowPrice,
                        Close = k.ClosePrice,
                        Volume = k.Volume
                    })
                    .ToList();

                // Thêm vào đầu list (vì lấy ngược từ hiện tại về quá khứ)
                allCandles.InsertRange(0, candles);
                endTime = klines.First().OpenTime.AddMilliseconds(-1);

                Console.WriteLine($"[Binance] Đã lấy {allCandles.Count}/{totalCandles} nến...");

                // Tránh rate limit
                await Task.Delay(500);
            }

            Console.WriteLine($"[Binance] Hoàn thành! Tổng {allCandles.Count} nến từ {allCandles.First().Time:dd/MM/yyyy} đến {allCandles.Last().Time:dd/MM/yyyy}");
            return allCandles.Take(totalCandles).OrderBy(c => c.Time).ToList();
        }
    }
}
