using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;

namespace MLTrain.Models
{
    public class ModelOutput
    {
        // Sửa từ float sang string để khớp với nhãn đã huấn luyện
        [ColumnName("PredictedLabel")]
        public string Prediction { get; set; }

        // Mảng này chứa xác suất của từng nhãn (0, 1, 2)
        public float[] Score { get; set; }
    }
}
