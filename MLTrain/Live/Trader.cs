using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Binance.Net.Enums;
using MLTrain.Core;
using MLTrain.Logs;
using MLTrain.Models;
using MLTrain.Services;
using Newtonsoft.Json.Linq;

namespace MLTrain.Live
{
    public static class Trader
    {
        static DateTime _currentCandle5m = DateTime.MinValue;
        private static string _symbol = "ETHUSDT";
        private static readonly object _candleLock = new();

        // FIX #1: Dùng ManualResetEventSlim để báo hiệu nến mới an toàn
        private static volatile bool _isNewCandle5m = false;

        // FIX #2: Tách flag sync riêng để không block lẫn nhau
        private static volatile bool _isSyncing = false;

        private readonly static int maxCandleKeep5m = 15000;
        private readonly static int maxCandleKeep1h = 10000;
        private readonly static int maxCandleKeep1d = 5000;

        private readonly static int maxGetCandle5m = 1000;
        private readonly static int maxGetCandle1h = 1000;
        private readonly static int maxGetCandle1d = 1000;

        public static async Task RunLive(
            string symbol,
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            _symbol = symbol;

            var mlService = new MLService();
            mlService.Load("model.zip");

            decimal minSL = 0.004m;
            decimal minTP = 0.008m;
            decimal fee = 0.001m;

            bool inTrade = false;
            bool isLong = false;
            decimal entryPrice = 0;
            decimal slPrice = 0;
            decimal tpPrice = 0;
            string side = "";

            InitCurrentCandles(candles5m, candles1h, candles1d);
            StartSyncTask(symbol, candles5m, candles1h, candles1d);

            await foreach (var (price, quantity) in ListenTradesAsync(symbol))
            {
                // FIX #1: Capture flag TRƯỚC khi UpdateAllCandles reset nó
                bool newCandle;
                lock (_candleLock)
                {
                    UpdateAllCandlesInternal(candles5m, candles1h, candles1d, price, quantity);
                    newCandle = _isNewCandle5m;
                    _isNewCandle5m = false; // reset sau khi đã đọc
                }

                if (inTrade)
                {
                    bool hitSL = isLong ? price <= slPrice : price >= slPrice;
                    bool hitTP = isLong ? price >= tpPrice : price <= tpPrice;

                    if (hitSL)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {side} | ❌ SL HIT | " +
                                          $"Entry: {entryPrice:F2} | SL: {slPrice:F2} | Giá: {price:F2}");
                        Console.ResetColor();

                        string slMessage = $"""
                            ❌ **SL HIT** ❌
                            {side}
                            Entry: {entryPrice:F2}
                            SL: {slPrice:F2}
                            Giá đóng: {price:F2}
                            Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                            """;
                        await TelegramBot.SendTelegramMessage(slMessage);
                        inTrade = false;
                        continue;
                    }

                    if (hitTP)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {side} | ✅ TP HIT | " +
                                          $"Entry: {entryPrice:F2} | TP: {tpPrice:F2} | Giá: {price:F2}");
                        Console.ResetColor();

                        string tpMessage = $"""
                            ✅ **TP HIT** ✅
                            {side}
                            Entry: {entryPrice:F2}
                            TP: {tpPrice:F2}
                            Giá đóng: {price:F2}
                            Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                            """;
                        await TelegramBot.SendTelegramMessage(tpMessage);
                        inTrade = false;
                        continue;
                    }
                }

                // FIX #1: Dùng biến local đã capture, không đọc lại _isNewCandle5m
                if (!newCandle) continue;
                if (inTrade) continue;

                int i = candles5m.Count - 2;
                if (i < 50) continue;

                var f = FeatureBuilder.Build(candles5m, i, candles1h, candles1d);
                if (f == null) continue;

                var (signal, confidence) = mlService.PredictFull(f);
                if (confidence <= 0.75f || (signal != "1" && signal != "2")) continue;

                entryPrice = candles5m[i].Close;
                isLong = signal == "1";
                side = isLong ? "LONG 🚀" : "SHORT 🔻";

                decimal atrRaw = (decimal)f.Atr;
                decimal slPct = atrRaw > 0 ? Math.Max(atrRaw / entryPrice, minSL) : minSL;
                decimal tpPct = atrRaw > 0 ? Math.Max(atrRaw / entryPrice * 2m, minTP) : minTP;

                slPrice = isLong
                    ? entryPrice * (1m - slPct - fee)
                    : entryPrice * (1m + slPct + fee);
                tpPrice = isLong
                    ? entryPrice * (1m + tpPct - fee)
                    : entryPrice * (1m - tpPct + fee);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {side} | MỞ LỆNH | " +
                                  $"Entry: {entryPrice:F2} | TP: {tpPrice:F2} | SL: {slPrice:F2} | " +
                                  $"Conf: {confidence:P0} | Trend1D: {f.Trend1D:F3} | Trend1H: {f.Trend1H:F3}");
                Console.ResetColor();

                string openMessage = $"""
                    🚀 **MỞ LỆNH {side}** 🚀
                    Symbol: {symbol}
                    Entry: {entryPrice:F2}
                    TP: {tpPrice:F2}
                    SL: {slPrice:F2}
                    Confidence: {confidence:P0}
                    Trend1D: {f.Trend1D:F3}
                    Trend1H: {f.Trend1H:F3}
                    ATR: {atrRaw:F4}
                    Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                    """;
                await TelegramBot.SendTelegramMessage(openMessage);

                inTrade = true;
            }
        }

        // FIX #3: StartSyncTask không cần lock bên ngoài Task.Run
        public static void StartSyncTask(
            string symbol,
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            _ = Task.Run(async () =>
            {
                var dataService = new DataService();
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    try
                    {
                        // Fetch bên ngoài lock (I/O không cần lock)
                        var fresh5m = await dataService.GetCandlesAsync(symbol, KlineInterval.FiveMinutes, totalCandles: maxGetCandle5m);
                        var fresh1h = await dataService.GetCandlesAsync(symbol, KlineInterval.OneHour, totalCandles: maxGetCandle1h);
                        var fresh1d = await dataService.GetCandlesAsync(symbol, KlineInterval.OneDay, totalCandles: maxGetCandle1d);

                        // Chỉ lock khi ghi vào list
                        lock (_candleLock)
                        {
                            SyncCandles(candles5m, fresh5m, maxCandleKeep5m);
                            SyncCandles(candles1h, fresh1h, maxCandleKeep1h);
                            SyncCandles(candles1d, fresh1d, maxCandleKeep1d);
                            InitCurrentCandles(candles5m, candles1h, candles1d);
                        }

                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] 🔄 Periodic sync hoàn tất");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SYNC ERROR] {ex.Message}");
                    }
                }
            });
        }

        // FIX #4: Tách thành internal method (gọi bên trong lock từ RunLive)
        private static void UpdateAllCandlesInternal(
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d,
            decimal price,
            decimal quantity)
        {
            var now = DateTime.UtcNow;
            var time5m = new DateTime(now.Year, now.Month, now.Day,
                                      now.Hour, (now.Minute / 5) * 5, 0,
                                      DateTimeKind.Utc);

            // ── Nến 5M mới ──────────────────────────────────────────
            if (time5m != _currentCandle5m)
            {
                if (_currentCandle5m != DateTime.MinValue)
                {
                    _isNewCandle5m = true;

                    // FIX #2: Chỉ trigger async sync nếu KHÔNG đang sync
                    if (!_isSyncing)
                        _ = SyncAllCandlesAsync(candles5m, candles1h, candles1d);
                    else
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] ⚠️ Sync bị skip (đang chạy), sẽ sync ở chu kỳ sau");
                }

                _currentCandle5m = time5m;

                var existing5m = candles5m.FirstOrDefault(c => c.Time == time5m);
                if (existing5m == null)
                {
                    candles5m.Add(new Candle
                    {
                        Time = time5m,
                        Open = price,
                        High = price,
                        Low = price,
                        Close = price,
                        Volume = quantity
                    });
                    if (candles5m.Count > maxCandleKeep5m)
                        candles5m.RemoveAt(0);
                }
            }

            // ── Cập nhật nến hiện tại ────────────────────────────────
            var current = candles5m.LastOrDefault(c => c.Time == time5m);
            if (current != null)
            {
                if (price > current.High) current.High = price;
                if (price < current.Low) current.Low = price;
                current.Close = price;
                current.Volume += quantity;
            }
        }

        // FIX #2: async sync — fetch ngoài lock, ghi trong lock
        private static async Task SyncAllCandlesAsync(
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var dataService = new DataService();

                // I/O ngoài lock
                var fresh5m = await dataService.GetCandlesAsync(_symbol, KlineInterval.FiveMinutes, totalCandles: maxGetCandle5m);
                var fresh1h = await dataService.GetCandlesAsync(_symbol, KlineInterval.OneHour, totalCandles: maxGetCandle1h);
                var fresh1d = await dataService.GetCandlesAsync(_symbol, KlineInterval.OneDay, totalCandles: maxGetCandle1d);

                // Chỉ lock khi ghi
                lock (_candleLock)
                {
                    SyncCandles(candles5m, fresh5m, maxCandleKeep5m);
                    SyncCandles(candles1h, fresh1h, maxCandleKeep1h);
                    SyncCandles(candles1d, fresh1d, maxCandleKeep1d);
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] 🔄 Candle sync hoàn tất");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC ERROR] {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        // FIX #5: SyncCandles hiệu quả hơn — dùng Dictionary, không Sort trong loop
        static void SyncCandles(List<Candle> existing, List<Candle> fresh, int maxKeep)
        {
            var lookup = existing.ToDictionary(c => c.Time);
            bool needsSort = false;

            foreach (var newCandle in fresh)
            {
                if (lookup.TryGetValue(newCandle.Time, out var found))
                {
                    found.High = newCandle.High;
                    found.Low = newCandle.Low;
                    found.Close = newCandle.Close;
                    found.Volume = newCandle.Volume;
                }
                else
                {
                    existing.Add(newCandle);
                    lookup[newCandle.Time] = newCandle;
                    needsSort = true;
                }
            }

            if (needsSort)
                existing.Sort((a, b) => a.Time.CompareTo(b.Time));

            // Trim một lần sau cùng
            while (existing.Count > maxKeep)
                existing.RemoveAt(0);
        }

        public static void InitCurrentCandles(
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            if (candles5m.Any())
                _currentCandle5m = candles5m.Last().Time;
        }

        public static async IAsyncEnumerable<(decimal price, decimal quantity)> ListenTradesAsync(
            string symbol,
            string urlStream = "wss://fstream.binance.com/ws")
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri($"{urlStream}/{symbol.ToLower()}@aggTrade"), CancellationToken.None);

            var buffer = new byte[8192];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var obj = JObject.Parse(json);

                var price = decimal.Parse((string)obj["p"]!);
                var quantity = decimal.Parse((string)obj["q"]!);

                yield return (price, quantity);
            }
        }
    }
}