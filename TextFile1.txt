using System;
using HedgeBot.Models;

namespace HedgeBot
{
    public class TradingSignalPro
    {
        // ============================================================
        //  CẤU HÌNH THÔNG SỐ (TỐI ƯU CHO KHUNG 3m - 5m ĐA COIN)
        // ============================================================
        public class StrategySettings
        {
            // --- Sideway & Trend Filter (Dùng ATR thay cho %) ---
            public decimal MinGapAtrMult = 0.5m;
            public decimal MinSlopeEma200 = 0.00008m;
            public decimal MinSlopeEma9 = 0.0002m;

            // --- RSI (ĐÃ TỐI ƯU: Siết biên độ để chống đu đỉnh/đáy) ---
            public decimal RsiShortMax = 45m; // Giảm nhẹ từ 48 để Short sớm hơn một chút
            public decimal RsiShortMin = 30m; // NÂNG LÊN từ 20: Chặn Short khi giá đã quá bán (Oversold)
            public decimal RsiLongMin = 55m;  // Nâng nhẹ từ 52 để đảm bảo lực tăng rõ
            public decimal RsiLongMax = 75m;  // HẠ XUỐNG từ 82: Chặn Long khi giá đã quá mua (Overbought)

            // --- FOMO filter (ĐÃ TỐI ƯU: Thắt chặt khoảng cách Entry) ---
            // Nếu giá vọt xa EMA9 hơn X lần ATR -> Bỏ qua (Chờ hồi mã thương)
            public decimal FomoAtrMultShort = 1.5m; // Siết lại từ 2.0
            public decimal FomoAtrMultLong = 1.8m;  // Siết lại từ 2.5

            // [MỚI] Chống Short ngay đáy / Long ngay đỉnh: 
            public decimal MaxDistFromEma200Mult = 6.0m;

            // [MỚI] Xác nhận hướng đi: EMA9 phải dốc đủ mạnh so với ATR
            public decimal MinSlopeAtrRatio = 0.05m;
        }

        private static readonly StrategySettings Config = new StrategySettings();

        // ============================================================
        //  ENTRY POINT
        // ============================================================
        public static (string? Side, string? PosSide) GetSignal(IndicatorData ind, decimal currentPrice)
        {
            // --- [KHỞI TẠO & LOG CHUNG] ---
            decimal gapAbs = Math.Abs(ind.EMA9 - ind.EMA21);

            // [CẢI TIẾN] Tính Slope dựa trên ATR để đồng bộ cho mọi loại giá Coin
            decimal ema9SlopeRaw = ind.EMA9 - ind.EMA9Prev;
            decimal ema9SlopeAtrRatio = ema9SlopeRaw / (ind.ATR + 0.00000001m);

            decimal ema9SlopePct = (ind.EMA9 - ind.EMA9Prev) / ind.EMA9Prev;
            decimal ema200SlopePct = ind.EMA200SlopeSmooth != 0 ? ind.EMA200SlopeSmooth : (ind.EMA200 - ind.EMA200Prev) / ind.EMA200Prev;
            decimal macdHistDelta = ind.MacdHist - ind.MacdHistPrev;

            Logger.Write($"[BÁO CÁO PHÂN TÍCH] - {DateTime.Now:HH:mm:ss} | Giá: {currentPrice:F6} | ATR: {ind.ATR:F6}");

            // --- BƯỚC 1: KIỂM TRA TREND ---
            bool isShortForm = ind.EMA9 < ind.EMA21 && ind.EMA21 < ind.EMA200;
            bool isLongForm = ind.EMA9 > ind.EMA21 && ind.EMA21 > ind.EMA200;

            Logger.Write("\n--- [I. CẤU TRÚC XU HƯỚNG (TREND)] ---");
            if (!isLongForm && !isShortForm)
            {
                Logger.Write("❌ TRẠNG THÁI: HỖN LOẠN (EMA chưa xếp đúng thứ tự)");
                return (null, null);
            }
            Logger.Write(isLongForm ? "✅ TRẠNG THÁI: TREND TĂNG" : "✅ TRẠNG THÁI: TREND GIẢM");

            // --- BƯỚC 2: KIỂM TRA SIDEWAY ---
            if (IsSideway(gapAbs, ema9SlopePct, ind.ATR))
            {
                Logger.Write("⏹️ KẾT LUẬN: Bỏ qua do Sideway.");
                return (null, null);
            }

            // --- BƯỚC 3: FOMO & REVERSAL FILTER ---
            Logger.Write("\n--- [III. BỘ LỌC FOMO & ĐẢO CHIỀU] ---");
            decimal distAbs = isLongForm ? (currentPrice - ind.EMA9) : (ind.EMA9 - currentPrice);
            decimal maxFomo = ind.ATR * (isLongForm ? Config.FomoAtrMultLong : Config.FomoAtrMultShort);

            if (distAbs > maxFomo)
            {
                Logger.Write($"❌ FOMO: {distAbs:F6} > {maxFomo:F6}. Giá chạy quá xa EMA9, dễ dính hồi mã thương!");
                return (null, null);
            }

            // [MỚI] KIỂM TRA KHOẢNG CÁCH EMA200 (CHỐNG BẮT ĐÁY/ĐỈNH DÀI HẠN)
            decimal distFromEma200 = Math.Abs(currentPrice - ind.EMA200);
            decimal maxRev = ind.ATR * Config.MaxDistFromEma200Mult;
            if (distFromEma200 > maxRev)
            {
                Logger.Write($"❌ RỦI RO: Giá quá xa EMA200 ({distFromEma200:F6} > {maxRev:F6}). Dễ đảo chiều mạnh!");
                return (null, null);
            }

            // [MỚI] KIỂM TRA HƯỚNG DỐC EMA9 THEO ATR (XÁC NHẬN LỰC ĐẨY)
            if (isLongForm && ema9SlopeAtrRatio < Config.MinSlopeAtrRatio)
            {
                Logger.Write($"❌ CẢNH BÁO: EMA9 Slope yếu ({ema9SlopeAtrRatio:F4} < {Config.MinSlopeAtrRatio}). Lực tăng chưa đủ!");
                return (null, null);
            }
            if (isShortForm && ema9SlopeAtrRatio > -Config.MinSlopeAtrRatio)
            {
                Logger.Write($"❌ CẢNH BÁO: EMA9 Slope yếu ({ema9SlopeAtrRatio:F4} > -{Config.MinSlopeAtrRatio}). Lực giảm chưa đủ!");
                return (null, null);
            }

            // [MỚI] KIỂM TRA RÂU NẾN (PRICE ACTION - CHỐNG REJECTION TẠI ĐỈNH/ĐÁY)
            decimal candleBody = Math.Abs(ind.Close - ind.Open);
            decimal upperWick = ind.High - Math.Max(ind.Open, ind.Close);
            decimal lowerWick = Math.Min(ind.Open, ind.Close) - ind.Low;

            if (isLongForm && upperWick > candleBody * 0.5m)
            {
                Logger.Write($"❌ BỊ TỪ CHỐI: Râu trên quá dài ({upperWick:F6}). Phe Bán đang ép đỉnh!");
                return (null, null);
            }
            if (isShortForm && lowerWick > candleBody * 0.5m)
            {
                Logger.Write($"❌ BỊ TỪ CHỐI: Râu dưới quá dài ({lowerWick:F6}). Phe Mua đang bắt đáy!");
                return (null, null);
            }
            // --- BƯỚC 4: XÁC NHẬN CHỈ BÁO ---
            Logger.Write("\n--- [IV. XÁC NHẬN CHỈ BÁO CHI TIẾT] ---");

            if (isLongForm)
            {
                Logger.Write("🎯 MỤC TIÊU: Đang tìm điểm vào lệnh >>> LONG <<<");

                bool c1 = currentPrice > ind.EMA9;
                bool c2 = ema200SlopePct > Config.MinSlopeEma200;
                bool c3 = ind.RSI > Config.RsiLongMin && ind.RSI < Config.RsiLongMax;
                bool c4 = ind.MacdHist > 0 && macdHistDelta > 0;

                Logger.Write($"   {(c1 ? "✅" : "❌")} 1. Giá trên EMA9 : {currentPrice:F6} > {ind.EMA9:F6}");
                Logger.Write($"   {(c2 ? "✅" : "❌")} 2. Slope EMA200 : {ema200SlopePct:P5} > {Config.MinSlopeEma200:P5}");
                Logger.Write($"   {(c3 ? "✅" : "❌")} 3. RSI ({Config.RsiLongMin}-{Config.RsiLongMax}): {ind.RSI:F2}");
                Logger.Write($"   {(c4 ? "✅" : "❌")} 4. MACD Hist + : {ind.MacdHist:F6} (Delta: {macdHistDelta:F6} > 0)");

                if (c1 && c2 && c3 && c4)
                {
                    Logger.Write("🚀 [TÍN HIỆU]: HỘI TỤ ĐỦ - THỰC THI LỆNH LONG!");
                    Logger.WriteToFile("🔻 [TÍN HIỆU]: HỘI TỤ ĐỦ - THỰC THI LỆNH LONG!");
                    return ("BUY", "LONG");
                }
            }
            else if (isShortForm)
            {
                Logger.Write("🎯 MỤC TIÊU: Đang tìm điểm vào lệnh >>> SHORT <<<");

                bool c1 = currentPrice < ind.EMA9;
                bool c2 = ema200SlopePct < -Config.MinSlopeEma200;
                bool c3 = ind.RSI < Config.RsiShortMax && ind.RSI > Config.RsiShortMin;
                bool c4 = ind.MacdHist < 0 && macdHistDelta < 0;

                Logger.Write($"   {(c1 ? "✅" : "❌")} 1. Giá dưới EMA9 : {currentPrice:F6} < {ind.EMA9:F6}");
                Logger.Write($"   {(c2 ? "✅" : "❌")} 2. Slope EMA200 : {ema200SlopePct:P5} < -{Config.MinSlopeEma200:P5}");
                Logger.Write($"   {(c3 ? "✅" : "❌")} 3. RSI ({Config.RsiShortMin}-{Config.RsiShortMax}): {ind.RSI:F2}");
                Logger.Write($"   {(c4 ? "✅" : "❌")} 4. MACD Hist - : {ind.MacdHist:F6} (Delta: {macdHistDelta:F6} < 0)");

                if (c1 && c2 && c3 && c4)
                {
                    Logger.Write("🔻 [TÍN HIỆU]: HỘI TỤ ĐỦ - THỰC THI LỆNH SHORT!");
                    Logger.WriteToFile("🔻 [TÍN HIỆU]: HỘI TỤ ĐỦ - THỰC THI LỆNH SHORT!");
                    return ("SELL", "SHORT");
                }
            }

            Logger.Write("\n🛑 TỔNG KẾT: Đứng ngoài (Không đủ điều kiện hoặc bị chặn bởi RSI/Râu nến).");
            return (null, null);
        }

        private static bool IsSideway(decimal gapAbs, decimal slope9Pct, decimal atr)
        {
            decimal minRequiredGap = atr * Config.MinGapAtrMult;
            bool gapTooSmall = gapAbs < minRequiredGap;
            bool slopeTooFlat = Math.Abs(slope9Pct) < Config.MinSlopeEma9;

            if (gapTooSmall || slopeTooFlat)
            {
                decimal gapPct = (gapAbs / (minRequiredGap + 0.0000001m)) * 100;
                decimal slopePct = (Math.Abs(slope9Pct) / (Config.MinSlopeEma9 + 0.0000001m)) * 100;

                Logger.Write($"⏸ [SIDEWAY] Gap: {gapPct:F0}% {(gapPct >= 100 ? "✅" : "❌")} | Slope9: {slopePct:F0}% {(slopePct >= 100 ? "✅" : "❌")}");
                return true;
            }
            return false;
        }
    }
}