using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HedgeBot.Models;
using Newtonsoft.Json.Linq;

namespace HedgeBot.Common
{
    static class Helpers
    {

        // 1. Lấy danh sách closes
        public static async Task<List<Candle>?> GetKlinesData(string symbol,string interval, int limit)
        {
            List<BinanceKline> klines = await BinanceApi.GetKlines(symbol,interval, limit);

            if (klines == null || klines.Count == 0)
                return null;

            // Chuyển đổi dữ liệu thô từ API sang List<Candle>
            return klines.Select(x => new Candle
            {
                Open = x.Open,
                High = (decimal)x.High,
                Low = (decimal)x.Low,
                Close = (decimal)x.Close,
                Volume = (decimal)x.Volume
            }).ToList();
        }

        // Cập nhật Tuple để trả về cả các giá trị Prev
        // Thêm tham số fullCandles vào hàm
        public static IndicatorData CalculateIndicators(List<decimal> closes, List<decimal> volumes, List<Candle> fullCandles)
        {
            // --- 1. LẤY DỮ LIỆU NẾN VỪA ĐÓNG (Nến ^2) ---
            // Nến ^1 là nến đang chạy, nến ^2 là nến đã hoàn tất để lấy tín hiệu chuẩn xác.
            var signalCandle = fullCandles.Count >= 2 ? fullCandles[^2] : null;

            // --- 2. TÍNH TOÁN CÁC CHỈ BÁO CƠ BẢN ---
            var ema9S = Calculates.CalculateEMASeries(closes, 9);
            var ema21S = Calculates.CalculateEMASeries(closes, 21);
            var ema200S = Calculates.CalculateEMASeries(closes, 200);
            var macd = Calculates.CalculateMACD(closes, 12, 26, 9);

            // --- 3. TÍNH EMA200 SLOPE SMOOTH (Độ dốc trung bình 5 nến) ---
            decimal ema200SlopeSmooth = 0;
            if (ema200S.Count >= 7)
            {
                decimal recent = ema200S[^2];
                decimal back5 = ema200S[^7];
                ema200SlopeSmooth = (recent - back5) / (back5 * 5);
            }

            // --- 4. TÍNH ATR (AVERAGE TRUE RANGE - 14 PERIODS) ---
            decimal atrValue = 0;
            int atrPeriod = 14;
            if (fullCandles.Count > atrPeriod + 1)
            {
                List<decimal> trList = new List<decimal>();
                // Tính TR cho 14 cây nến kết thúc tại nến signalCandle ([^2])
                for (int i = fullCandles.Count - atrPeriod - 1; i < fullCandles.Count - 1; i++)
                {
                    var current = fullCandles[i];
                    var prev = fullCandles[i - 1];

                    decimal tr = Math.Max(current.High - current.Low,
                                 Math.Max(Math.Abs(current.High - prev.Close),
                                          Math.Abs(current.Low - prev.Close)));
                    trList.Add(tr);
                }
                atrValue = trList.Average();
            }

            // --- 5. ĐÓNG GÓI DỮ LIỆU VÀO MODEL INDICATORDATA ---
            return new IndicatorData
            {
                // Thông tin nến để TradingSignalPro kiểm tra Price Action (Râu nến)
                Open = signalCandle?.Open ?? 0,
                High = signalCandle?.High ?? 0,
                Low = signalCandle?.Low ?? 0,
                Close = signalCandle?.Close ?? 0,

                // EMA Values
                EMA9 = ema9S.Count >= 2 ? ema9S[^2] : 0,
                EMA21 = ema21S.Count >= 2 ? ema21S[^2] : 0,
                EMA200 = ema200S.Count >= 2 ? ema200S[^2] : 0,
                EMA9Prev = ema9S.Count >= 3 ? ema9S[^3] : 0,
                EMA200Prev = ema200S.Count >= 3 ? ema200S[^3] : 0,

                // Trend & Momentum
                EMA200SlopeSmooth = ema200SlopeSmooth,
                RSI = Calculates.RSI(closes, 14),
                MacdHist = macd.Histogram.Count >= 2 ? macd.Histogram[^2] : 0,
                MacdHistPrev = macd.Histogram.Count >= 3 ? macd.Histogram[^3] : 0,

                // Volatility
                ATR = atrValue
            };
        }

        private static decimal CalculateATR(List<Candle> candles, int period, int shiftFromEnd)
        {
            int endIndex = candles.Count - shiftFromEnd;
            int startIndex = endIndex - period;

            if (startIndex <= 0) return 0;

            decimal sumTR = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                var cur = candles[i];
                var prev = candles[i - 1];

                decimal tr = Math.Max(cur.High - cur.Low,
                             Math.Max(Math.Abs(cur.High - prev.Close),
                                      Math.Abs(cur.Low - prev.Close)));

                sumTR += tr;
            }

            return sumTR / period;
        }
    }
}
