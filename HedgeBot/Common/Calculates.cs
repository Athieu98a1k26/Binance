using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot.Common
{
    static class Calculates
    {
        public static (List<decimal> MacdLine, List<decimal> SignalLine, List<decimal> Histogram) CalculateMACD(List<decimal> closes, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            var emaFast = CalculateEMASeries(closes, fastPeriod);
            var emaSlow = CalculateEMASeries(closes, slowPeriod);

            // Xác định số lượng phần tử tối thiểu mà cả 2 cùng có (thường là emaSlow)
            int count = Math.Min(emaFast.Count, emaSlow.Count);
            if (count == 0) return (new List<decimal>(), new List<decimal>(), new List<decimal>());

            List<decimal> macdLine = new List<decimal>();

            // Chỉ chạy vòng lặp cho những nến mà cả Fast và Slow đều đã tính xong
            // Lấy 'count' phần tử cuối cùng của mỗi danh sách
            for (int i = 0; i < count; i++)
            {
                // emaFast[^count + i] lấy ngược từ cuối lên để khớp vị trí thời gian với emaSlow
                macdLine.Add(emaFast[emaFast.Count - count + i] - emaSlow[emaSlow.Count - count + i]);
            }

            // Tính Signal Line từ MacdLine vừa tạo
            var signalLine = CalculateEMASeries(macdLine, signalPeriod);

            // Tiếp tục đồng bộ MacdLine và SignalLine để tính Histogram
            int countHist = Math.Min(macdLine.Count, signalLine.Count);
            List<decimal> histogram = new List<decimal>();

            for (int i = 0; i < countHist; i++)
            {
                histogram.Add(macdLine[macdLine.Count - countHist + i] - signalLine[signalLine.Count - countHist + i]);
            }

            return (macdLine, signalLine, histogram);
        }



        // Tính toàn bộ chuỗi EMA
        public static List<decimal> CalculateEMASeries(List<decimal> closes, int period)
        {
            if (closes.Count < period) return new List<decimal>();

            var ema = new List<decimal>(closes.Count);
            decimal multiplier = 2m / (period + 1);

            // Phần đầu dùng SMA
            decimal sum = 0;
            for (int i = 0; i < period; i++)
                sum += closes[i];

            decimal prevEma = sum / period;
            ema.Add(prevEma);

            // Tính các phần sau
            for (int i = period; i < closes.Count; i++)
            {
                decimal currentEma = (closes[i] - prevEma) * multiplier + prevEma;
                ema.Add(currentEma);
                prevEma = currentEma;
            }

            return ema;
        }

        public static decimal RSI(List<decimal> prices, int period = 14)
        {
            // Kiểm tra điều kiện đầu vào: Cần ít nhất (period + 1) nến để tính RSI
            if (prices == null || prices.Count <= period)
                return 50m; // Trả về mức trung bình nếu không đủ dữ liệu

            decimal gain = 0;
            decimal loss = 0;

            // 1. Tính toán giá trị trung bình ban đầu (Initial SMA of Gains/Losses)
            for (int i = 1; i <= period; i++)
            {
                // Lấy dữ liệu nến kết thúc tại nến [^2] (tương ứng với signalCandle)
                // Lưu ý: index tính ngược từ nến đã đóng
                int idx = prices.Count - period + i - 1;
                decimal diff = prices[idx] - prices[idx - 1];

                if (diff >= 0) gain += diff;
                else loss -= diff;
            }

            decimal avgGain = gain / period;
            decimal avgLoss = loss / period;

            // 2. Tính toán theo phương pháp Wilder's Smoothing (Chuẩn RSI hiện đại)
            // Nếu bạn muốn tính cho các nến tiếp theo để mượt hơn, có thể chạy loop.
            // Ở đây ta tính giá trị RSI tại thời điểm nến đóng gần nhất ([^2]).

            if (avgLoss == 0) return 100m; // Tránh lỗi chia cho 0 nếu giá chỉ có tăng

            // Công thức RSI: 100 - (100 / (1 + RS))
            // RS = AvgGain / AvgLoss
            decimal rs = avgGain / avgLoss;
            decimal rsi = 100m - (100m / (1m + rs));

            return Math.Round(rsi, 2);
        }
    }
}
