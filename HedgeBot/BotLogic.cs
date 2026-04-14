using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using HedgeBot.Common;
using HedgeBot.Models;
using Newtonsoft.Json.Linq;

namespace HedgeBot
{
    public class BotLogic
    {
        // =================== RUN BOT CHÍNH ===================
        public static async Task Run(decimal currentPrice,string symbol,string interval = "3m")
        {
            // 1. Lấy danh sách nến đầy đủ
            var candles = await Helpers.GetKlinesData(symbol,interval, 500);

            if (candles == null || candles.Count < 250)
            {
                Logger.Write($"[{symbol}] ⏳ Không đủ dữ liệu nến.");
                return;
            }

            // 2. Tách Closes và Volumes để dùng cho các hàm tính EMA/MACD/RSI cũ
            var closes = candles.Select(x => x.Close).ToList();
            var volumes = candles.Select(x => x.Volume).ToList();

            // 3. Tính Indicators (Lưu ý: Truyền thêm candles vào tham số thứ 3)
            var indicators = Helpers.CalculateIndicators(closes, volumes, candles);

            // 4. Xác định tín hiệu
            TradingSignalPro.GetSignal(indicators, currentPrice);
        }
    }
}