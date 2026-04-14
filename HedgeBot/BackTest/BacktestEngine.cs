using HedgeBot.Common;
using HedgeBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot.BackTest
{
    public class BacktestEngine
    {
        public static void RunTest(List<Candle> history)
        {
            string logFilePath = "backtest_result_details.csv";
            int warmUp = 500;

            // 1. Dọn dẹp file cũ
            if (File.Exists(logFilePath)) File.Delete(logFilePath);

            // 2. Khởi tạo biến thống kê
            int wins = 0;
            int losses = 0;
            decimal totalEthPoints = 0; // Tổng số "giá" ETH ăn được
            decimal feeRate = 0.0004m;  // Phí sàn 0.04% (Commission)

            // 3. Header mới chi tiết hơn
            // Cột 'Eth_Points': số giá ETH kiếm được/mất đi nòng cốt
            string header = "OpenTime,Side,EntryPrice,ExitPrice,Result,Eth_Points,Profit_After_Fee,RSI,ATR";
            File.WriteAllLines(logFilePath, new[] { header });

            Console.WriteLine("\n🚀 ĐANG CHẠY BACKTEST CHI TIẾT...");

            for (int i = warmUp; i < history.Count; i++)
            {
                var currentContext = history.GetRange(i - warmUp, warmUp);
                var currentCandle = history[i];

                var closes = currentContext.Select(x => x.Close).ToList();
                var volumes = currentContext.Select(x => x.Volume).ToList();
                var indicators = Helpers.CalculateIndicators(closes, volumes, currentContext);

                var (side, posSide) = TradingSignalPro.GetSignal(indicators, currentCandle.Close);

                if (side != null)
                {
                    decimal entryPrice = currentCandle.Close;
                    // Giả lập TP 1% và SL 0.5% (Bạn có thể sửa tỷ lệ này)
                    decimal tpPrice = posSide == "LONG" ? entryPrice * 1.01m : entryPrice * 0.99m;
                    decimal slPrice = posSide == "LONG" ? entryPrice * 0.995m : entryPrice * 1.005m;

                    string result = "LOSE";
                    decimal exitPrice = slPrice;
                    decimal ethPoints = 0;

                    // --- KIỂM TRA TƯƠNG LAI (XÁC ĐỊNH WIN/LOSS) ---
                    for (int j = i + 1; j < history.Count; j++)
                    {
                        var f = history[j];
                        if (posSide == "LONG")
                        {
                            if (f.High >= tpPrice) { result = "WIN"; exitPrice = tpPrice; break; }
                            if (f.Low <= slPrice) { result = "LOSE"; exitPrice = slPrice; break; }
                        }
                        else // SHORT
                        {
                            if (f.Low <= tpPrice) { result = "WIN"; exitPrice = tpPrice; break; }
                            if (f.High >= slPrice) { result = "LOSE"; exitPrice = slPrice; break; }
                        }
                    }

                    // Tính toán lợi nhuận
                    if (result == "WIN") wins++; else losses++;

                    // Số giá ETH ăn được (ví dụ: Long từ 2500 lên 2525 là +25 giá)
                    ethPoints = posSide == "LONG" ? (exitPrice - entryPrice) : (entryPrice - exitPrice);
                    totalEthPoints += ethPoints;

                    // Lợi nhuận % sau khi trừ phí
                    decimal profitPct = ((ethPoints / entryPrice) * 100) - (feeRate * 2 * 100);

                    // 4. Ghi Log chi tiết từng lệnh
                    string tradeLog = string.Format("{0:yyyy-MM-dd HH:mm:ss},{1},{2:F2},{3:F2},{4},{5:F2},{6:F2}%,{7:F2},{8:F6}",
                        currentCandle.OpenTime,
                        posSide,
                        entryPrice,
                        exitPrice,
                        result,
                        ethPoints,
                        profitPct,
                        indicators.RSI,
                        indicators.ATR
                    );
                    File.AppendAllLines(logFilePath, new[] { tradeLog });
                }
            }

            // 5. Xuất báo cáo tổng kết ra Console
            int totalTrades = wins + losses;
            decimal winRate = totalTrades > 0 ? (decimal)wins / totalTrades * 100 : 0;

            Console.WriteLine("\n================ BÁO CÁO BACKTEST ================");
            Console.WriteLine($"📊 Tổng số lệnh: {totalTrades}");
            Console.WriteLine($"✅ Thắng: {wins} | ❌ Thua: {losses}");
            Console.WriteLine($"🎯 Tỷ lệ Win: {winRate:F2}%");
            Console.WriteLine($"💰 Tổng giá ETH ăn được: {totalEthPoints:F2} USD/1 ETH");
            Console.WriteLine($"📁 Chi tiết lưu tại: {Path.GetFullPath(logFilePath)}");
            Console.WriteLine("==================================================");
        }
    }
}
