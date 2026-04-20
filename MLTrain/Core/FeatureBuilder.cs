using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MLTrain.Models;

namespace MLTrain.Core
{
    public static class FeatureBuilder
    {
        public static ModelInput Build(List<Candle> candles, int i)
        {
            // Chúng ta cần ít nhất 50 nến quá khứ để tính EMA21 và RSI14 ổn định
            int lookback = 50;
            if (i < lookback) return null;

            // Lấy một khung cửa sổ dữ liệu nhỏ để tính toán cho nhanh, thay vì lấy hết candles
            var window = candles.GetRange(i - lookback, lookback + 1);
            var closes = window.Select(x => x.Close).ToList();
            var volumes = window.Select(x => x.Volume).ToList();

            var emaFast = CalcEMA(closes, 9);
            var emaFastPrev = CalcEMA(closes.Take(closes.Count - 1).ToList(), 9);
            var emaSlow = CalcEMA(closes, 21);

            return new ModelInput
            {
                Rsi = (float)CalcRSI(closes, 14),
                EmaFast = (float)emaFast,
                EmaSlow = (float)emaSlow,
                // Độ dốc EMA tính theo %, giúp model hiểu được xu hướng tăng/giảm mạnh hay yếu
                EmaSlope = emaFastPrev == 0 ? 0f : (float)((emaFast - emaFastPrev) / emaFastPrev * 100),
                Atr = (float)CalcATR(window, window.Count - 1, 14),
                // So sánh Volume nến hiện tại với trung bình 20 nến trước
                VolumeSpike = (float)(volumes.Last() / (volumes.Skip(volumes.Count - 21).Take(20).Average() + 0.0000001m))
            };
        }

        private static decimal CalcEMA(List<decimal> v, int p)
        {
            if (v.Count < p) return 0m;
            decimal k = 2m / (p + 1);
            decimal ema = v.Take(p).Average();

            for (int i = p; i < v.Count; i++)
                ema = v[i] * k + ema * (1 - k);

            return ema;
        }

        private static decimal CalcRSI(List<decimal> v, int p)
        {
            if (v.Count < p + 1) return 0m;
            decimal gain = 0m, loss = 0m;

            for (int i = v.Count - p; i < v.Count; i++)
            {
                var d = v[i] - v[i - 1];
                if (d > 0) gain += d;
                else loss -= d;
            }

            if (loss == 0m) return 100m;
            decimal rs = (gain / p) / (loss / p);
            return 100m - (100m / (1 + rs));
        }

        private static decimal CalcATR(List<Candle> c, int idx, int p)
        {
            if (idx < p) return 0m;
            decimal sumTR = 0m;

            for (int i = idx - p + 1; i <= idx; i++)
            {
                var cur = c[i];
                var prev = c[i - 1];
                decimal tr = Math.Max(cur.High - cur.Low,
                             Math.Max(Math.Abs(cur.High - prev.Close),
                                      Math.Abs(cur.Low - prev.Close)));
                sumTR += tr;
            }
            return sumTR / p;
        }
    }
}
