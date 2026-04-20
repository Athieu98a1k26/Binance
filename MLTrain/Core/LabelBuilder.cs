using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MLTrain.Models;

namespace MLTrain.Core
{
    public static class LabelBuilder
    {
        public static decimal Build(List<Candle> candles, int i)
        {
            int lookahead = 10; // Kiểm tra 10 nến tiếp theo
            if (i + lookahead >= candles.Count) return 0;

            var entry = candles[i].Close;

            // Cài đặt Target Profit (TP) và Stop Loss (SL)
            // Với Futures, bạn nên tính cả phí (khoảng 0.04% - 0.1% tùy mức VIP)
            decimal tpPercent = 0.01m; // 1%
            decimal slPercent = 0.01m; // 1%

            decimal longTP = entry * (1 + tpPercent);
            decimal longSL = entry * (1 - slPercent);

            decimal shortTP = entry * (1 - tpPercent);
            decimal shortSL = entry * (1 + slPercent);

            // Duyệt qua từng nến tương lai để xem cái gì xảy ra trước
            for (int j = i + 1; j <= i + lookahead; j++)
            {
                var high = candles[j].High;
                var low = candles[j].Low;

                // --- KIỂM TRA LỆNH LONG ---
                bool longHitTP = high >= longTP;
                bool longHitSL = low <= longSL;

                // --- KIỂM TRA LỆNH SHORT ---
                bool shortHitTP = low <= shortTP;
                bool shortHitSL = high >= shortSL;

                // Ưu tiên: Nếu nến biến động mạnh chạm cả TP và SL thì coi như rủi ro (bỏ qua)
                if (longHitTP && longHitSL) return 0;

                if (longHitTP) return 1; // LONG thắng
                if (shortHitTP) return 2; // SHORT thắng

                // Nếu chạm SL của cả 2 phe trước khi chạm TP thì coi như thua
                if (longHitSL && shortHitSL) return 0;
            }

            return 0; // Không chạm mục tiêu nào trong 10 nến
        }
    }
}
