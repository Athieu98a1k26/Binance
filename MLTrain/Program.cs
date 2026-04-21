using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using Binance.Net.Enums;
using MLTrain.BackTests;
using MLTrain.Core;
using MLTrain.Live;
using MLTrain.Logs;
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
        //Console.WriteLine("📥 Đang tải dữ liệu 3 timeframe...");

        //List<Candle> candles5m = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.FiveMinutes, totalCandles: 2000000);
        //List<Candle> candles1h = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneHour, totalCandles: 1000000);
        //List<Candle> candles1d = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneDay, totalCandles: 500000);

        //Console.WriteLine($"✅ 5M: {candles5m.Count} nến | 1H: {candles1h.Count} nến | 1D: {candles1d.Count} nến");

        //// =====================================================================
        //// BƯỚC 2: Chia tập Train / Test theo nến 5M
        ////         1H và 1D được slice tương ứng theo thời gian
        //// =====================================================================
        //int testSize = 50000;
        //int trainSize = candles5m.Count - testSize;

        //// --- Nến 5M ---
        //var train5m = candles5m.GetRange(0, trainSize);
        //// test5m dùng toàn bộ allCandles để BacktestDetailed có đủ context quá khứ
        //// (tương tự code gốc: truyền allCandles + startIndex)

        //// --- Nến 1H: lấy toàn bộ (BacktestDetailed tự tìm đúng timestamp) ---
        //// --- Nến 1D: tương tự ---
        //// Không cần slice 1H/1D vì chúng ta dùng FindLastIndex theo Time

        //DateTime trainCutoff = train5m.Last().Time;

        //// Nến 1H dùng cho train: chỉ lấy những nến trước trainCutoff
        //var train1h = candles1h.Where(c => c.Time <= trainCutoff).ToList();
        //var train1d = candles1d.Where(c => c.Time <= trainCutoff).ToList();

        //Console.WriteLine($"\n📊 Phân chia dữ liệu:");
        //Console.WriteLine($"   Train 5M : {train5m.Count} nến  ({train5m[0].Time:dd/MM/yyyy} → {train5m.Last().Time:dd/MM/yyyy})");
        //Console.WriteLine($"   Test  5M : {testSize} nến cuối ({candles5m[trainSize].Time:dd/MM/yyyy} → {candles5m.Last().Time:dd/MM/yyyy})");
        //Console.WriteLine($"   Train 1H : {train1h.Count} nến");
        //Console.WriteLine($"   Train 1D : {train1d.Count} nến");

        // =====================================================================
        // BƯỚC 3: Train
        // =====================================================================
        //Console.WriteLine("\n🧠 Bắt đầu huấn luyện...");
        //await Trainer.TrainWithData(train5m, train1h, train1d);

        //// =====================================================================
        //// BƯỚC 4: Backtest trên tập Test (AI chưa từng thấy)
        //// =====================================================================
        //Console.WriteLine("\n=== BẮT ĐẦU KIỂM TRA TRÊN DỮ LIỆU MỚI ===");
        //BackTestFuture.BacktestDetailed(candles5m, candles1h, candles1d, startIndex: trainSize);
        
        // ── Bước 3: Chạy live ──
        Console.WriteLine("\n🚀 Bắt đầu live trading...");
        Console.WriteLine("📥 Đang tải dữ liệu 3 timeframe...");

        var candles5m = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.FiveMinutes, totalCandles: 200000);
        var candles1h = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneHour, totalCandles: 100000);
        var candles1d = await dataService.GetCandlesAsync(SYMBOL, KlineInterval.OneDay, totalCandles: 50000);

        
        await Trader.RunLive(SYMBOL, candles5m, candles1h, candles1d);

        Console.ReadLine();
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