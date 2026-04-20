using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLTrain.Models
{
    public class ModelInput
    {
        // ML.NET yêu cầu các đặc trưng đầu vào phải là Float (Single)
        public float Rsi { get; set; }
        public float EmaFast { get; set; }
        public float EmaSlow { get; set; }
        public float EmaSlope { get; set; }
        public float Atr { get; set; }
        public float VolumeSpike { get; set; }

        // Label dùng string để làm phân loại (Classification)
        public string Label { get; set; }
    }
}
