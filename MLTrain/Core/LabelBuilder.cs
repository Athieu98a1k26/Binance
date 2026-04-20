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
            int lookahead = 10;
            if (i + lookahead >= candles.Count) return 0;

            decimal entry = candles[i].Close;
            decimal tpPercent = 0.01m;
            decimal slPercent = 0.01m;

            decimal longTP = entry * (1 + tpPercent);
            decimal longSL = entry * (1 - slPercent);
            decimal shortTP = entry * (1 - tpPercent);
            decimal shortSL = entry * (1 + slPercent);

            bool longStopped = false;
            bool shortStopped = false;

            for (int j = i + 1; j <= i + lookahead; j++)
            {
                var high = candles[j].High;
                var low = candles[j].Low;

                // Kiểm tra SL trước
                if (!longStopped && low <= longSL) longStopped = true;
                if (!shortStopped && high >= shortSL) shortStopped = true;

                // Nếu cả 2 đều stop thì out
                if (longStopped && shortStopped) return 0;

                // Kiểm tra TP chỉ khi chưa bị stop
                bool longHitTP = !longStopped && high >= longTP;
                bool shortHitTP = !shortStopped && low <= shortTP;

                // Trong cùng 1 nến, nếu cả 2 TP cùng xảy ra -> hòa
                if (longHitTP && shortHitTP) return 0;

                if (longHitTP) return 1;
                if (shortHitTP) return 2;
            }
            return 0;
        }
    }
}
