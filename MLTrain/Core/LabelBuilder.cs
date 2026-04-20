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
            const int LOOKAHEAD = 10;
            const decimal TP_PERCENT = 0.01m; // 1%
            const decimal SL_PERCENT = 0.01m; // 1%

            // Kiểm tra biên
            if (i < 0 || i + LOOKAHEAD >= candles.Count)
                return 0;

            var entry = candles[i].Close;

            // --- Mức giá cụ thể ---
            decimal longTP = entry * (1 + TP_PERCENT);
            decimal longSL = entry * (1 - SL_PERCENT);
            decimal shortTP = entry * (1 - TP_PERCENT);
            decimal shortSL = entry * (1 + SL_PERCENT);

            // Duyệt tương lai
            for (int j = i + 1; j <= i + LOOKAHEAD; j++)
            {
                var c = candles[j];

                // === QUAN TRỌNG: XÁC ĐỊNH CÁI GÌ ĐẾN TRƯỚC TRONG CÂY NẾN ===
                // Dựa vào bóng nến để đoán xu hướng giá chạy trong phiên
                bool isBullishCandle = c.Close >= c.Open;

                // TH1: NẾN TĂNG (Giá chạy từ Open -> Low -> High -> Close)
                if (isBullishCandle)
                {
                    // Giá đi xuống trước (tạo đáy)
                    if (c.Low <= longSL) return 0; // LONG THUA (Chạm SL trước)
                    if (c.Low <= shortTP) return 2; // SHORT THẮNG (Chạm TP trước)

                    // Sau đó giá mới đi lên
                    if (c.High >= longTP) return 1; // LONG THẮNG
                    if (c.High >= shortSL) return 0; // SHORT THUA
                }
                // TH2: NẾN GIẢM (Giá chạy từ Open -> High -> Low -> Close)
                else
                {
                    // Giá đi lên trước (tạo đỉnh)
                    if (c.High >= shortSL) return 0; // SHORT THUA (Chạm SL trước)
                    if (c.High >= longTP) return 1;   // LONG THẮNG (Chạm TP trước)

                    // Sau đó giá mới đi xuống
                    if (c.Low <= longSL) return 0; // LONG THUA
                    if (c.Low <= shortTP) return 2; // SHORT THẮNG
                }

                // Trường hợp đặc biệt: Nến Doji không râu (ít xảy ra)
                // Nếu High/Low chạm cùng lúc hoặc khó xác định -> Bỏ qua
            }

            return 0; // Không chạm mục tiêu nào trong lookahead
        }
    }
}
