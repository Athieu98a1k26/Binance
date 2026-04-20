using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MLTrain.Models;

namespace MLTrain.Core
{
    public class SignalEngine
    {
        private MLService _ml;
        private List<Candle> _candles = new();

        public SignalEngine(MLService ml)
        {
            _ml = ml;
        }

        public SignalResult OnNewCandle(Candle candle)
        {
            _candles.Add(candle);

            // 1. Chỉ giữ lại số lượng nến cần thiết để tiết kiệm RAM (ví dụ 200 nến gần nhất)
            if (_candles.Count > 500) _candles.RemoveAt(0);

            // 2. Kiểm tra điều kiện tối thiểu để tính toán Feature
            if (_candles.Count < 100) return null;

            // 3. Xây dựng Feature cho nến vừa đóng
            var f = FeatureBuilder.Build(_candles, _candles.Count - 1);
            if (f == null) return null;

            // 4. Dự đoán từ AI
            // Lưu ý: signal ở đây nên là string ("0", "1", "2") lấy từ r.Prediction
            var (signal, conf) = _ml.PredictFull(f);

            // 5. LỌC TÍN HIỆU (Phần quan trọng nhất)

            // Lọc theo độ tự tin (Confidence) - 0.55 có thể hơi thấp, nên thử 0.6 hoặc 0.65
            if (conf < 0.60f) return null;

            // Nếu AI báo nhãn "0" (No Trade) thì thoát luôn
            if (signal == "0") return null;

            // Lọc theo xu hướng (Regime Filter) - Đảm bảo AI không đánh ngược trend lớn
            if (!RegimeDetector.IsTrending(f)) return null;

            // 6. Trả về kết quả
            return new SignalResult
            {
                // So sánh chuỗi để gán nhãn cho chính xác
                Signal = signal == "1" ? "LONG" : signal == "2" ? "SHORT" : "NONE",
                Confidence = conf,
                Time = candle.Time
            };
        }
    }
}
