using HedgeBot;
using HedgeBot.BackTest;
using HedgeBot.Common;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string symbol = "ETHUSDT";


        string interval = "3m";
        string fileName = "history_6_months.csv";

        // BƯỚC 1: LẤY DỮ LIỆU TỪ API (Chỉ cần chạy 1 lần đầu)
        if (!File.Exists(fileName))
        {
            DateTime SixMonthAgo = DateTime.UtcNow.AddMonths(-24);
            await DataTool.DownloadHistoryToCsv(symbol, interval, SixMonthAgo, fileName);
        }

        // BƯỚC 2: LẤY DỮ LIỆU TỪ CSV RA ĐỂ TEST
        var historyData = DataTool.LoadFromCsv(fileName);

        // BƯỚC 3: CHẠY BACKTEST
        BacktestEngine.RunTest(historyData);

        Console.WriteLine("Nhấn phím bất kỳ để thoát...");
        Console.ReadKey();

        //decimal currentPrice = 0;
        //// 1. Chạy một Task riêng để cập nhật giá liên tục từ Stream
        //_ = Task.Run(async () =>
        //{
        //    await foreach (decimal price in ListenTradesAsync(symbol))
        //    {
        //        currentPrice = price;
        //    }
        //});

        //// 2. Vòng lặp Logic bây giờ sẽ chạy được vì Stream đã ở luồng khác
        //while (true)
        //{
        //    if (currentPrice == 0)
        //    {
        //        await Task.Delay(1000); // Đợi Stream kết nối thành công
        //        continue;
        //    }

        //    try
        //    {
        //        await BotLogic.Run(currentPrice, symbol);
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Write($"❌ ERROR: {ex.Message}");
        //    }

        //    await Task.Delay(3000); // Chạy ổn định mỗi 3 giây
        //}

    }

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
}