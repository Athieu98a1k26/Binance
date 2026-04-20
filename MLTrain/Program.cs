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
        const string SYMBOL = "ETHUSDT";
        var dataService = new DataService();

        // =====================================================================
        // BƯỚC 1: Tải dữ liệu 3 khung thời gian
        // =====================================================================
        Console.WriteLine("📥 Đang tải dữ liệu 3 timeframe...");

        var candles5m = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.FiveMinutes, totalCandles: 1000000);
        var candles1h = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneHour, totalCandles: 500000);
        var candles1d = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneDay, totalCandles: 200000);

        Console.WriteLine($"✅ 5M: {candles5m.Count} nến | 1H: {candles1h.Count} nến | 1D: {candles1d.Count} nến");

        // =====================================================================
        // BƯỚC 2: Chia tập Train / Test theo nến 5M
        //         1H và 1D được slice tương ứng theo thời gian
        // =====================================================================
        int testSize = 50000;
        int trainSize = candles5m.Count - testSize;

        // --- Nến 5M ---
        var train5m = candles5m.GetRange(0, trainSize);
        // test5m dùng toàn bộ allCandles để BacktestDetailed có đủ context quá khứ
        // (tương tự code gốc: truyền allCandles + startIndex)

        // --- Nến 1H: lấy toàn bộ (BacktestDetailed tự tìm đúng timestamp) ---
        // --- Nến 1D: tương tự ---
        // Không cần slice 1H/1D vì chúng ta dùng FindLastIndex theo Time

        DateTime trainCutoff = train5m.Last().Time;

        // Nến 1H dùng cho train: chỉ lấy những nến trước trainCutoff
        var train1h = candles1h.Where(c => c.Time <= trainCutoff).ToList();
        var train1d = candles1d.Where(c => c.Time <= trainCutoff).ToList();

        Console.WriteLine($"\n📊 Phân chia dữ liệu:");
        Console.WriteLine($"   Train 5M : {train5m.Count} nến  ({train5m[0].Time:dd/MM/yyyy} → {train5m.Last().Time:dd/MM/yyyy})");
        Console.WriteLine($"   Test  5M : {testSize} nến cuối ({candles5m[trainSize].Time:dd/MM/yyyy} → {candles5m.Last().Time:dd/MM/yyyy})");
        Console.WriteLine($"   Train 1H : {train1h.Count} nến");
        Console.WriteLine($"   Train 1D : {train1d.Count} nến");

        // =====================================================================
        // BƯỚC 3: Train
        // =====================================================================
        Console.WriteLine("\n🧠 Bắt đầu huấn luyện...");
        await Trainer.TrainWithData(train5m, train1h, train1d);

        // =====================================================================
        // BƯỚC 4: Backtest trên tập Test (AI chưa từng thấy)
        // =====================================================================
        Console.WriteLine("\n=== BẮT ĐẦU KIỂM TRA TRÊN DỮ LIỆU MỚI ===");
        BacktestDetailed(candles5m, candles1h, candles1d, startIndex: trainSize);

        Console.ReadLine();
    }

    // =========================================================================
    // BACKTEST CHI TIẾT
    // Nhận đủ 3 timeframe để FeatureBuilder tính được đầy đủ context
    // =========================================================================
    public static void BacktestDetailed(
        List<Candle> candles5m,
        List<Candle> candles1h,
        List<Candle> candles1d,
        int startIndex)
    {
        var mlService = new MLService();
        mlService.Load("model.zip");

        int longWins = 0, longLosses = 0;
        int shortWins = 0, shortLosses = 0;
        int skipped = 0;

        // TP/SL động theo ATR — giá trị fallback nếu ATR quá nhỏ
        decimal minSL = 0.004m; // 0.4%
        decimal minTP = 0.008m; // 0.8%
        decimal fee = 0.001m; // 0.1% tổng phí 2 chiều

        for (int i = startIndex; i < candles5m.Count - 50; i++)
        {
            var f = FeatureBuilder.Build(candles5m, i, candles1h, candles1d);
            if (f == null) { skipped++; continue; }

            var (signal, confidence) = mlService.PredictFull(f);

            if (confidence <= 0.75f || (signal != "1" && signal != "2")) continue;

            decimal entryPrice = candles5m[i].Close;
            bool isLong = signal == "1";
            string side = isLong ? "LONG 🚀" : "SHORT 🔻";

            // TP/SL động: dùng ATR từ feature, fallback về min nếu ATR quá bé
            decimal atrRaw = (decimal)f.Atr;
            decimal slPct = atrRaw > 0 ? Math.Max(atrRaw / entryPrice, minSL) : minSL;
            decimal tpPct = atrRaw > 0 ? Math.Max(atrRaw / entryPrice * 2m, minTP) : minTP;

            decimal slPrice = isLong
                ? entryPrice * (1m - slPct - fee)
                : entryPrice * (1m + slPct + fee);
            decimal tpPrice = isLong
                ? entryPrice * (1m + tpPct - fee)
                : entryPrice * (1m - tpPct + fee);

            bool tradeResolved = false;
            for (int j = i + 1; j < candles5m.Count; j++)
            {
                var next = candles5m[j];
                bool hitSL = isLong ? next.Low <= slPrice : next.High >= slPrice;
                bool hitTP = isLong ? next.High >= tpPrice : next.Low <= tpPrice;

                // Nến biến động cực mạnh: chạm cả 2 — tính là thua (worst case)
                if (hitSL && hitTP) hitTP = false;

                if (hitSL)
                {
                    if (isLong) longLosses++; else shortLosses++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"[{next.Time:dd/MM HH:mm}] {side} | ❌ SL | " +
                        $"Entry: {entryPrice:F2} | SL: {slPrice:F2} | " +
                        $"Lỗ: -{slPct:P2} | Conf: {confidence:P0} | " +
                        $"Trend1D: {f.Trend1D:F3} | Trend1H: {f.Trend1H:F3}");
                    Console.ResetColor();
                    i = j; tradeResolved = true; break;
                }

                if (hitTP)
                {
                    if (isLong) longWins++; else shortWins++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(
                        $"[{next.Time:dd/MM HH:mm}] {side} | ✅ TP | " +
                        $"Entry: {entryPrice:F2} | TP: {tpPrice:F2} | " +
                        $"Lãi: +{tpPct:P2} | Conf: {confidence:P0} | " +
                        $"Trend1D: {f.Trend1D:F3} | Trend1H: {f.Trend1H:F3}");
                    Console.ResetColor();
                    i = j; tradeResolved = true; break;
                }
            }

            // Nếu không resolve (hết data), bỏ qua
            if (!tradeResolved) skipped++;
        }

        PrintSummary(longWins, longLosses, shortWins, shortLosses, skipped);
    }

    // =========================================================================
    // BÁO CÁO TỔNG KẾT
    // =========================================================================
    private static void PrintSummary(int lW, int lL, int sW, int sL, int skipped = 0)
    {
        int totalLong = lW + lL;
        int totalShort = sW + sL;
        int total = totalLong + totalShort;
        double wrLong = totalLong > 0 ? (double)lW / totalLong : 0;
        double wrShort = totalShort > 0 ? (double)sW / totalShort : 0;
        double wrTotal = total > 0 ? (double)(lW + sW) / total : 0;

        // Tính PnL ước tính (TP = 1.6%, SL = 0.8% — xấp xỉ)
        double pnlEst = (lW + sW) * 1.6 - (lL + sL) * 0.8;

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("📊  BÁO CÁO BACKTEST CHI TIẾT");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"LONG",-8}| Thắng: {lW,4} | Thua: {lL,4} | Winrate: {wrLong,7:P2} | Tổng: {totalLong}");
        Console.WriteLine($"{"SHORT",-8}| Thắng: {sW,4} | Thua: {sL,4} | Winrate: {wrShort,7:P2} | Tổng: {totalShort}");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"TỔNG",-8}| Thắng: {lW + sW,4} | Thua: {lL + sL,4} | Winrate: {wrTotal,7:P2} | Tổng lệnh: {total}");
        Console.WriteLine($"         PnL ước tính (không dùng margin): {pnlEst:+0.00;-0.00}%");
        Console.WriteLine($"         Bỏ qua (không đủ data): {skipped}");
        Console.WriteLine(new string('-', 60));

        // Cảnh báo lệch phe
        if (totalLong > 5 && totalShort > 5 && Math.Abs(wrLong - wrShort) > 0.2)
        {
            string weakSide = wrLong < wrShort ? "LONG" : "SHORT";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  CẢNH BÁO: Model yếu ở phe {weakSide} (chênh lệch > 20%).");
            Console.WriteLine("    → Kiểm tra lại phân phối Label trong tập Train.");
            Console.ResetColor();
        }

        // Cảnh báo ít lệnh
        if (total < 30)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Chỉ có {total} lệnh — hạ ngưỡng confidence (hiện 0.75) để có thêm mẫu.");
            Console.ResetColor();
        }

        // Đánh giá kết quả
        Console.WriteLine();
        if (wrTotal >= 0.55 && pnlEst > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅  Kết quả TÍCH CỰC — có thể chạy live với size nhỏ để kiểm chứng.");
        }
        else if (wrTotal >= 0.45)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Kết quả TRUNG BÌNH — cần thêm feature hoặc lọc trend chặt hơn.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌  Kết quả KÉM — không nên chạy live. Xem lại LabelBuilder và Feature.");
        }
        Console.ResetColor();
        Console.WriteLine(new string('=', 60));
    }

    // =========================================================================
    // WEBSOCKET LIVE (giữ nguyên từ bản gốc)
    // =========================================================================
    public static async IAsyncEnumerable<decimal> ListenTradesAsync(
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
            yield return decimal.Parse((string)obj["p"]!);
        }
    }
}