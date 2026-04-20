using Microsoft.ML.Data;

namespace MLTrain.Models
{
    public class ModelInput
    {
        // ── Feature từ nến 5M ────────────────────────────────────────────────
        public float Rsi { get; set; }
        public float EmaFast { get; set; }
        public float EmaSlow { get; set; }
        public float EmaSlope { get; set; } // % thay đổi EMA9
        public float Atr { get; set; }
        public float VolumeSpike { get; set; } // volume / avg20

        // ── Feature đa khung thời gian (MỚI) ────────────────────────────────
        public float Trend1H { get; set; } // % slope EMA9 nến 1H
        public float Trend1D { get; set; } // % slope EMA9 nến 1D
        public float RsiH1 { get; set; } // RSI14 nến 1H
        public float AtrRatio { get; set; } // ATR(5M) / ATR(1H)

        // ── Nhãn: "0" = skip, "1" = LONG, "2" = SHORT ───────────────────────
        public string Label { get; set; } = "0";
    }
}