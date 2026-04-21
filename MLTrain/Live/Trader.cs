using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Binance.Net.Enums;
using MLTrain.Core;
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

        private static bool _isSyncing = false;
        static bool _isNewCandle5m = false;

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

            // Trạng thái lệnh hiện tại
            bool inTrade = false;
            bool isLong = false;
            decimal entryPrice = 0;
            decimal slPrice = 0;
            decimal tpPrice = 0;
            string side = "";

            Trader.InitCurrentCandles(candles5m, candles1h, candles1d);
            // Khởi động sync background
            StartSyncTask(_symbol, candles5m, candles1h, candles1d);

            await foreach (var (price, quantity) in Trader.ListenTradesAsync(_symbol))
            {
                UpdateAllCandles(candles5m, candles1h, candles1d, price, quantity);

                // ── Kiểm tra TP/SL theo giá real-time (không cần đợi nến đóng) ──
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

                        //await PlaceOrderAsync(SYMBOL, isLong ? Side.Sell : Side.Buy, 0.01m); // đóng lệnh
                        inTrade = false;
                        continue;
                    }

                    if (hitTP)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {side} | ✅ TP HIT | " +
                                          $"Entry: {entryPrice:F2} | TP: {tpPrice:F2} | Giá: {price:F2}");
                        Console.ResetColor();

                        //await PlaceOrderAsync(SYMBOL, isLong ? Side.Sell : Side.Buy, 0.01m); // đóng lệnh
                        inTrade = false;
                        continue;
                    }
                }

                // ── Chỉ phân tích khi nến 5M vừa đóng ──────────────────────────
                if (!Trader._isNewCandle5m) continue;
                if (inTrade) continue; // đang có lệnh → không mở thêm

                // Dùng nến đã đóng [^2], không dùng nến đang hình thành [^1]
                int i = candles5m.Count - 2;
                if (i < 50) continue;

                var f = FeatureBuilder.Build(candles5m, i, candles1h, candles1d);
                if (f == null) continue;

                var (signal, confidence) = mlService.PredictFull(f);
                if (confidence <= 0.75f || (signal != "1" && signal != "2")) continue;

                // ── Tính TP/SL động theo ATR (giống BacktestDetailed) ───────────
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

                // ── Đặt lệnh ────────────────────────────────────────────────────
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {side} | MỞ LỆNH | " +
                                  $"Entry: {entryPrice:F2} | TP: {tpPrice:F2} | SL: {slPrice:F2} | " +
                                  $"Conf: {confidence:P0} | Trend1D: {f.Trend1D:F3} | Trend1H: {f.Trend1H:F3}");
                Console.ResetColor();

                //await PlaceOrderAsync(SYMBOL, isLong ? Side.Buy : Side.Sell, 0.01m);
                inTrade = true;
            }
        }

        public static void StartSyncTask(string symbol, List<Candle> candles5m,
                           List<Candle> candles1h, List<Candle> candles1d)
        {
            lock (_candleLock)
            {
                var dataService = new DataService();

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5));
                        try
                        {
                            var fresh5m = await dataService.GetCandlesAsync(symbol, KlineInterval.FiveMinutes, totalCandles: maxGetCandle5m);
                            var fresh1h = await dataService.GetCandlesAsync(symbol, KlineInterval.OneHour, totalCandles: maxGetCandle1h);
                            var fresh1d = await dataService.GetCandlesAsync(symbol, KlineInterval.OneDay, totalCandles: maxGetCandle1d);

                            SyncCandles(candles5m, fresh5m, maxKeep: maxCandleKeep5m);
                            SyncCandles(candles1h, fresh1h, maxKeep: maxCandleKeep1h);
                            SyncCandles(candles1d, fresh1d, maxKeep: maxCandleKeep1d);

                            // Cập nhật lại _current* sau khi sync
                            Trader.InitCurrentCandles(candles5m, candles1h, candles1d);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SYNC ERROR] {ex.Message}");
                        }
                    }
                });
            }
            
        }


        public static void UpdateAllCandles(
             List<Candle> candles5m,
             List<Candle> candles1h,
             List<Candle> candles1d,
             decimal price,
             decimal quantity)
        {
            lock (_candleLock)
            {
                var now = DateTime.UtcNow;
                _isNewCandle5m = false;

                var time5m = new DateTime(now.Year, now.Month, now.Day,
                                           now.Hour, (now.Minute / 5) * 5, 0,
                                           DateTimeKind.Utc);

                // ── Nến 5M mới ───────────────────────────────────────
                if (time5m != _currentCandle5m)
                {
                    if (_currentCandle5m != DateTime.MinValue)
                    {
                        _isNewCandle5m = true;
                        _ = SyncAllCandlesAsync(candles5m, candles1h, candles1d);
                    }

                    _currentCandle5m = time5m;

                    // Kiểm tra nến đã có từ API load chưa (tránh sai Open)
                    var existing = candles5m.FirstOrDefault(c => c.Time == time5m);
                    if (existing == null)
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
                        if (candles5m.Count > maxCandleKeep5m) candles5m.RemoveAt(0);
                    }
                    // Nếu đã có (từ API) → chỉ cập nhật Close/High/Low/Volume bên dưới
                }

                // ── Cập nhật nến hiện tại ─────────────────────────────
                var current = candles5m.LastOrDefault(c => c.Time == time5m);
                if (current != null)
                {
                    if (price > current.High) current.High = price;
                    if (price < current.Low) current.Low = price;
                    current.Close = price;
                    current.Volume += quantity;
                }
            }
        }

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
                var fresh5m = await dataService.GetCandlesAsync(_symbol, KlineInterval.FiveMinutes, totalCandles: maxGetCandle5m);
                var fresh1h = await dataService.GetCandlesAsync(_symbol, KlineInterval.OneHour, totalCandles: maxCandleKeep1h);
                var fresh1d = await dataService.GetCandlesAsync(_symbol, KlineInterval.OneDay, totalCandles: maxCandleKeep1d);

                lock (_candleLock)
                {
                    SyncCandles(candles5m, fresh5m, maxKeep: maxCandleKeep5m);
                    SyncCandles(candles1h, fresh1h, maxKeep: maxCandleKeep1h);
                    SyncCandles(candles1d, fresh1d, maxKeep: maxCandleKeep1d);
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] 🔄 Sync hoàn tất");
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

        static void SyncCandles(List<Candle> existing, List<Candle> fresh, int maxKeep)
        {
            foreach (var newCandle in fresh)
            {
                var found = existing.FirstOrDefault(c => c.Time == newCandle.Time);
                if (found != null)
                {
                    // Ghi đè bằng data chính xác từ Binance
                    found.High = newCandle.High;
                    found.Low = newCandle.Low;
                    found.Close = newCandle.Close;
                    found.Volume = newCandle.Volume;
                }
                else
                {
                    // Nến bị miss do mất kết nối → thêm vào đúng thứ tự
                    existing.Add(newCandle);
                    existing.Sort((a, b) => a.Time.CompareTo(b.Time));
                    if (existing.Count > maxKeep)
                    {
                        existing.RemoveAt(0);
                    }
                }
            }
        }

        public static void InitCurrentCandles(
            List<Candle> candles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            if (candles5m.Any()) _currentCandle5m = candles5m.Last().Time;
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

                var price = decimal.Parse((string)obj["p"]!); // giá
                var quantity = decimal.Parse((string)obj["q"]!); // số lượng

                yield return (price, quantity);
            }
        }
    }
}
