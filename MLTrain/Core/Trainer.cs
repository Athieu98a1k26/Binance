using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML;
using MLTrain.Models;
using MLTrain.Services;

namespace MLTrain.Core
{
    public class Trainer
    {
        public static void Train(string modelPath, List<ModelInput> data)
        {
            var ml = new MLContext();

            var dv = ml.Data.LoadFromEnumerable(data);

            var pipeline = ml.Transforms.Conversion.MapValueToKey("Label")
                .Append(ml.Transforms.Concatenate("Features",
                    nameof(ModelInput.Rsi),
                    nameof(ModelInput.EmaFast),
                    nameof(ModelInput.EmaSlow),
                    nameof(ModelInput.EmaSlope),
                    nameof(ModelInput.Atr),
                    nameof(ModelInput.VolumeSpike)))
                .Append(ml.MulticlassClassification.Trainers.LightGbm())
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(dv);

            ml.Model.Save(model, dv.Schema, modelPath);

            Console.WriteLine("✅ Model saved: " + modelPath);
        }


        public static async Task TrainWithData(List<Candle> trainCandles)
        {
            Console.WriteLine($"=== TRAINING WITH {trainCandles.Count} CANDLES ===");
            var dataset = new List<ModelInput>();

            for (int i = 100; i < trainCandles.Count - 20; i++)
            {
                var f = FeatureBuilder.Build(trainCandles, i);
                if (f == null) continue;

                decimal labelValue = LabelBuilder.Build(trainCandles, i);
                f.Label = labelValue.ToString();
                dataset.Add(f);
            }

            Trainer.Train("model.zip", dataset);
            Console.WriteLine("✅ TRAIN DONE");
        }

        public static async Task TrainFromBinance()
        {
            Console.WriteLine("=== LOAD DATA FROM BINANCE ===");

            var dataService = new DataService();

            var candles = await dataService.GetCandlesAsync("BTCUSDT");

            Console.WriteLine($"Candles: {candles.Count}");

            var dataset = new List<ModelInput>();

            for (int i = 100; i < candles.Count - 20; i++)
            {
                var f = FeatureBuilder.Build(candles, i);
                if (f == null) continue;

                // Gọi hàm Build của bạn (trả về decimal: 0, 1, hoặc 2)
                decimal labelValue = LabelBuilder.Build(candles, i);

                // Gán vào ModelInput dưới dạng chuỗi để ML.NET hiểu đây là "Nhãn"
                f.Label = labelValue.ToString();

                dataset.Add(f);
            }


            Console.WriteLine($"Total: {dataset.Count}");
            Console.WriteLine($"Long (1): {dataset.Count(x => x.Label == "1")}");
            Console.WriteLine($"Short (2): {dataset.Count(x => x.Label == "2")}");
            Console.WriteLine($"No Trade (0): {dataset.Count(x => x.Label == "0")}");

            Console.WriteLine($"Dataset: {dataset.Count}");

            Trainer.Train("model.zip", dataset);

            Console.WriteLine("✅ TRAIN DONE");
        }
    }
}
