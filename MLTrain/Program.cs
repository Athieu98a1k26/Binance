using System.Net.WebSockets;
using System.Text;
using Binance.Net.Enums;
using MLTrain.Core;
using MLTrain.Models;
using MLTrain.Services;
using Newtonsoft.Json.Linq;

class Program
{
    static async Task Main()
    {
        var dataService = new DataService();
        // 1. Lấy toàn bộ nến từ Binance (ví dụ 10,000 nến)
        var allCandles = await dataService.GetCandlesAsync("ETHUSDT", totalCandles: 10000);

        // 2. Chia dữ liệu
        int testSize = 2000; // Giữ lại 1000 nến cuối cùng để Test
        int trainSize = allCandles.Count - testSize;

        // Lấy đoạn đầu để Train
        var trainCandles = allCandles.GetRange(0, trainSize);
        // Lấy đoạn cuối để Test
        var testCandles = allCandles.GetRange(trainSize, testSize);

        Console.WriteLine($"Tổng nến: {allCandles.Count}");
        Console.WriteLine($"Nến dùng để Train: {trainCandles.Count} (Từ {trainCandles[0].Time} đến {trainCandles.Last().Time})");
        Console.WriteLine($"Nến dùng để Test: {testCandles.Count} (Từ {testCandles[0].Time} đến {testCandles.Last().Time})");

        // 3. Tiến hành Train trên tập dữ liệu cũ
        await Trainer.TrainWithData(trainCandles);

        // 4. Tiến hành Backtest trên tập dữ liệu mới (AI chưa từng thấy)
        Console.WriteLine("\n=== BẮT ĐẦU KIỂM TRA TRÊN DỮ LIỆU MỚI ===");
        BacktestDetailed(allCandles, trainSize);

        Console.ReadLine();
    }


    public static void BacktestDetailed(List<Candle> allCandles, int startIndex)
    {
        var mlService = new MLService();
        mlService.Load("model.zip");

        // Thống kê chi tiết
        int longWins = 0, longLosses = 0;
        int shortWins = 0, shortLosses = 0;

        decimal slPercent = 0.008m; // 0.8% cho ETH
        decimal tpPercent = 0.016m; // 1.6%

        for (int i = startIndex; i < allCandles.Count - 50; i++)
        {
            var f = FeatureBuilder.Build(allCandles, i);
            if (f == null) continue;

            var (signal, confidence) = mlService.PredictFull(f);

            if (confidence > 0.75f && (signal == "1" || signal == "2"))
            {
                decimal entryPrice = allCandles[i].Close;
                bool isLong = signal == "1";
                string side = isLong ? "LONG 🚀" : "SHORT 🔻";

                decimal slPrice = isLong ? entryPrice * (1 - slPercent) : entryPrice * (1 + slPercent);
                decimal tpPrice = isLong ? entryPrice * (1 + tpPercent) : entryPrice * (1 - tpPercent);

                for (int j = i + 1; j < allCandles.Count; j++)
                {
                    var next = allCandles[j];
                    bool hitSL = isLong ? next.Low <= slPrice : next.High >= slPrice;
                    bool hitTP = isLong ? next.High >= tpPrice : next.Low <= tpPrice;

                    if (hitSL)
                    {
                        if (isLong) longLosses++; else shortLosses++;

                        // In chi tiết lỗi thua
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{next.Time:dd/MM HH:mm}] {side} | THẤT BẠI (SL) | Entry: {entryPrice:F2} | Exit: {slPrice:F2} | Lỗ: -{slPercent:P2}");
                        Console.ResetColor();

                        i = j; break;
                    }
                    if (hitTP)
                    {
                        if (isLong) longWins++; else shortWins++;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{next.Time:dd/MM HH:mm}] {side} | THÀNH CÔNG (TP)| Entry: {entryPrice:F2} | Exit: {tpPrice:F2} | Lãi: +{tpPercent:P2}");
                        Console.ResetColor();

                        i = j; break;
                    }
                }
            }
        }

        // --- BẢNG TỔNG KẾT CHI TIẾT ---
        PrintSummary(longWins, longLosses, shortWins, shortLosses);
    }

    private static void PrintSummary(int lW, int lL, int sW, int sL)
    {
        int totalLong = lW + lL;
        int totalShort = sW + sL;
        double wrLong = totalLong > 0 ? (double)lW / totalLong : 0;
        double wrShort = totalShort > 0 ? (double)sW / totalShort : 0;

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("📊 BÁO CÁO PHÂN TÍCH CHIẾN THUẬT");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"LONG  | Thắng: {lW} | Thua: {lL} | Winrate: {wrLong:P2}");
        Console.WriteLine($"SHORT | Thắng: {sW} | Thua: {sL} | Winrate: {wrShort:P2}");
        Console.WriteLine(new string('-', 50));

        // Cảnh báo nếu Model bị lệch
        if (Math.Abs(wrLong - wrShort) > 0.2)
        {
            string weakSide = wrLong < wrShort ? "LONG" : "SHORT";
            Console.WriteLine($"⚠️ CẢNH BÁO: Model đang yếu khi dự đoán phe {weakSide}.");
            Console.WriteLine("Lý do: Có thể tập dữ liệu Train đang bị lệch hoặc Feature chưa đủ tốt cho phe này.");
        }
        Console.WriteLine(new string('=', 50));
    }
    //static async Task Main()
    //{
    //    string symbol = "ETHUSDT";
    //    var dataService = new DataService();

    //    // 1. Tải 200 nến gần nhất để có đủ dữ liệu tính EMA/RSI (Feature)
    //    var candles = await dataService.GetCandlesAsync(symbol, KlineInterval.FiveMinutes, 200);

    //    var mlService = new MLService();
    //    mlService.Load("model.zip");

    //    Console.WriteLine($"--- ĐANG CHẠY LIVE: {symbol} ---");

    //    // 2. Bắt đầu lắng nghe luồng giá Real-time
    //    await foreach (var currentPrice in ListenTradesAsync(symbol))
    //    {
    //        // Cập nhật giá Close của nến cuối cùng (nến đang chạy)
    //        var lastCandle = candles.Last();
    //        lastCandle.Close = currentPrice;

    //        // Cứ mỗi khi giá thay đổi, ta có thể check AI (hoặc đợi nến đóng)
    //        // Ở đây mình check mỗi khi giá nhảy:
    //        var f = FeatureBuilder.Build(candles, candles.Count - 1);
    //        if (f == null) continue;

    //        var (signal, confidence) = mlService.PredictFull(f);

    //        // In ra màn hình để theo dõi
    //        Console.Title = $"Price: {currentPrice} | Signal: {signal} ({confidence:P0})";

    //        if (confidence > 0.80f && signal != "0")
    //        {
    //            string action = signal == "1" ? "🚀 LONG" : "🔻 SHORT";
    //            Logger.Write($"[{DateTime.Now:HH:mm:ss}] TÍN HIỆU: {action} | Giá: {currentPrice} | Conf: {confidence:P2}");

    //            // Ở ĐÂY LÀ NƠI GỌI API ĐẶT LỆNH THẬT
    //            // ExecuteOrder(action, currentPrice);
    //        }

    //        // 3. Logic chuyển nến: Nếu thời gian hiện tại đã sang nến mới, hãy thêm nến mới vào list
    //        if (DateTime.UtcNow >= lastCandle.Time.AddMinutes(5))
    //        {
    //            Console.WriteLine("--- MỞ NẾN 5M MỚI ---");
    //            candles.Add(new Candle { Time = lastCandle.Time.AddMinutes(5), Open = currentPrice });
    //            if (candles.Count > 300) candles.RemoveAt(0); // Giữ RAM sạch
    //        }
    //    }
    //}

    public static async IAsyncEnumerable<decimal> ListenTradesAsync(string symbol, string urlStream = "wss://fstream.binance.com/ws")
    {
        using ClientWebSocket ws = new ClientWebSocket();
        //wss://stream.testnet.binancefuture.com/ws/
        //wss://fstream.binance.com/ws/

        string url = $"{urlStream}/{symbol.ToLower()}@aggTrade";

        await ws.ConnectAsync(new Uri(url), CancellationToken.None);

        byte[] buffer = new byte[8192];

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            JObject obj = JObject.Parse(json);

            yield return decimal.Parse((string)obj["p"]!);
        }
    }

    public static async Task Backtest(List<Candle> testCandles)
    {
        var mlService = new MLService();
        mlService.Load("model.zip");

        foreach (var candle in testCandles)
        {
            // Lưu ý: FeatureBuilder cần danh sách nến để tính EMA/RSI, 
            // nhưng ở đây ta chỉ quan tâm kết quả của các nến trong tập Test
            int idx = testCandles.IndexOf(candle);
            var f = FeatureBuilder.Build(testCandles, idx);
            if (f == null) continue;

            var (signal, confidence) = mlService.PredictFull(f);

            if (confidence > 0.7f && signal != "0")
            {
                string action = signal == "1" ? "🚀 LONG" : "🔻 SHORT";
                Console.WriteLine($"[{candle.Time:dd/MM HH:mm}] Giá: {candle.Close:F2} | {action} | Conf: {confidence:P2}");
            }
        }
    }
    public static async Task Backtest(int numberOfCandles = 1000)
    {
        var dataService = new DataService();
        var candles = await dataService.GetCandlesAsync("BTCUSDT");

        var mlService = new MLService();
        mlService.Load("model.zip");

        Console.WriteLine($"--- Chạy thử trên {numberOfCandles} nến gần nhất ---");

        // Chạy vòng lặp từ (Count - numberOfCandles) đến nến cuối cùng
        for (int i = candles.Count - numberOfCandles; i < candles.Count; i++)
        {
            var currentFeature = FeatureBuilder.Build(candles, i);
            if (currentFeature == null) continue;

            var (signal, confidence) = mlService.PredictFull(currentFeature);
            var candle = candles[i];

            // Chỉ in lệnh khi thỏa mãn độ tự tin và không phải nhãn "0"
            if (confidence > 0.65f && signal != "0")
            {
                string action = signal == "1" ? "🚀 LONG " : "🔻 SHORT";

                // Định dạng: [Ngày/Tháng/Năm Giờ:Phút]
                // Ví dụ: [20/04/2026 01:15]
                string timeStr = candle.Time.ToString("dd/MM/yyyy HH:mm");

                Console.WriteLine($"[{timeStr}] Giá: {candle.Close:F2} | {action} | Độ tin cậy: {confidence:P2}");
            }
        }
    }
}