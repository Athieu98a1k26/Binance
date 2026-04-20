using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML;
using MLTrain.Models;

namespace MLTrain.Core
{
    public class MLService
    {
        private MLContext ml = new MLContext();
        private PredictionEngine<ModelInput, ModelOutput> engine;

        public void Load(string path)
        {
            var model = ml.Model.Load(path, out _);
            engine = ml.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);
        }

        public (string Label, float Confidence) PredictFull(ModelInput input)
        {
            var r = engine.Predict(input);

            // Lấy giá trị xác suất cao nhất
            var max = r.Score.Max();

            // Trả về Prediction (là "0", "1" hoặc "2") và độ tự tin
            return (r.Prediction, max);
        }

    }
}
