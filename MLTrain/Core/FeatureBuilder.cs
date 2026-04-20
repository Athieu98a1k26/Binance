using MLTrain.Models;

namespace MLTrain.Core
{
    public static class FeatureBuilder
    {
        /// <summary>
        /// Build feature cho nến 5M tại index i.
        /// Truyền candles1h và candles1d để lấy context xu hướng đa khung thời gian.
        /// </summary>
        public static ModelInput? Build(
            List<Candle> candles5m,
            int i,
            List<Candle>? candles1h = null,
            List<Candle>? candles1d = null)
        {
            const int lookback = 50;
            if (i < lookback) return null;

            // ── Feature từ 5M ────────────────────────────────────────────────
            var window = candles5m.GetRange(i - lookback, lookback + 1);
            var closes = window.Select(x => x.Close).ToList();
            var volumes = window.Select(x => x.Volume).ToList();

            var emaFast = CalcEMA(closes, 9);
            var emaFastPrev = CalcEMA(closes.Take(closes.Count - 1).ToList(), 9);
            var emaSlow = CalcEMA(closes, 21);
            var atr5m = CalcATR(window, window.Count - 1, 14);

            // ── Feature từ 1H ────────────────────────────────────────────────
            float trend1H = 0f;
            float rsiH1 = 50f;
            float atrRatio = 1f;

            if (candles1h != null && candles1h.Count >= 50)
            {
                var time5m = candles5m[i].Time;
                int idx1h = candles1h.FindLastIndex(c => c.Time <= time5m);

                if (idx1h >= 50)
                {
                    var closes1h = candles1h.GetRange(idx1h - 50, 51).Select(x => x.Close).ToList();
                    var emaF1h = CalcEMA(closes1h, 9);
                    var emaFPrev1h = CalcEMA(closes1h.Take(closes1h.Count - 1).ToList(), 9);

                    trend1H = emaFPrev1h == 0 ? 0f
                        : (float)((emaF1h - emaFPrev1h) / emaFPrev1h * 100m);
                    rsiH1 = (float)CalcRSI(closes1h, 14);

                    var window1h = candles1h.GetRange(idx1h - 14, 15);
                    var atr1h = CalcATR(window1h, window1h.Count - 1, 14);
                    if (atr1h > 0) atrRatio = (float)(atr5m / atr1h);
                }
            }

            // ── Feature từ 1D ────────────────────────────────────────────────
            float trend1D = 0f;

            if (candles1d != null && candles1d.Count >= 22)
            {
                var time5m = candles5m[i].Time;
                int idx1d = candles1d.FindLastIndex(c => c.Time <= time5m);

                if (idx1d >= 20)
                {
                    var closes1d = candles1d.GetRange(idx1d - 20, 21).Select(x => x.Close).ToList();
                    var emaF1d = CalcEMA(closes1d, 9);
                    var emaFPrev1d = CalcEMA(closes1d.Take(closes1d.Count - 1).ToList(), 9);

                    trend1D = emaFPrev1d == 0 ? 0f
                        : (float)((emaF1d - emaFPrev1d) / emaFPrev1d * 100m);
                }
            }

            return new ModelInput
            {
                Rsi = (float)CalcRSI(closes, 14),
                EmaFast = (float)emaFast,
                EmaSlow = (float)emaSlow,
                EmaSlope = emaFastPrev == 0 ? 0f
                              : (float)((emaFast - emaFastPrev) / emaFastPrev * 100m),
                Atr = (float)atr5m,
                VolumeSpike = (float)(volumes.Last()
                              / (volumes.Skip(volumes.Count - 21).Take(20).Average() + 0.0000001m)),
                Trend1H = trend1H,
                Trend1D = trend1D,
                RsiH1 = rsiH1,
                AtrRatio = atrRatio,
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
                if (d > 0) gain += d; else loss -= d;
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