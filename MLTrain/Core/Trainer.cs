using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;
using MLTrain.Models;

namespace MLTrain.Core
{
    public class Trainer
    {
        // ─── Hàm train lõi ──────────────────────────────────────────────────
        public static void Train(string modelPath, List<ModelInput> data)
        {
            var ml = new MLContext(seed: 42);
            var dv = ml.Data.LoadFromEnumerable(data);

            // In phân phối nhãn để kiểm tra cân bằng
            var dist = data.GroupBy(d => d.Label)
                           .OrderBy(g => g.Key)
                           .Select(g => $"  Nhãn {g.Key}: {g.Count()} mẫu ({g.Count() * 100.0 / data.Count:F1}%)");
            Console.WriteLine("📋 Phân phối nhãn trong dataset:");
            foreach (var line in dist) Console.WriteLine(line);

            var pipeline = ml.Transforms.Conversion.MapValueToKey("Label")
                .Append(ml.Transforms.Concatenate("Features",
                    nameof(ModelInput.Rsi),
                    nameof(ModelInput.EmaFast),
                    nameof(ModelInput.EmaSlow),
                    nameof(ModelInput.EmaSlope),
                    nameof(ModelInput.Atr),
                    nameof(ModelInput.VolumeSpike),
                    // Feature đa khung thời gian
                    nameof(ModelInput.Trend1H),
                    nameof(ModelInput.Trend1D),
                    nameof(ModelInput.RsiH1),
                    nameof(ModelInput.AtrRatio)))
                .Append(ml.MulticlassClassification.Trainers.LightGbm(
                    new LightGbmMulticlassTrainer.Options
                    {
                        NumberOfLeaves = 64,
                        MinimumExampleCountPerLeaf = 20,
                        LearningRate = 0.05,
                        NumberOfIterations = 300,
                        EarlyStoppingRound = 30,    // cần có validation set qua .Fit(data, validationData)
                        UseSoftmax = true
                    }))
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            Console.WriteLine("🔄 Đang huấn luyện LightGBM...");
            var model = pipeline.Fit(dv);
            ml.Model.Save(model, dv.Schema, modelPath);
            Console.WriteLine("✅ Model đã lưu: " + modelPath);
        }

        // ─── Hàm chuẩn bị dataset và gọi Train ─────────────────────────────
        public static async Task TrainWithData(
            List<Candle> trainCandles5m,
            List<Candle> candles1h,
            List<Candle> candles1d)
        {
            Console.WriteLine($"\n=== TRAINING: {trainCandles5m.Count} nến 5M ===");

            var dataset = new List<ModelInput>();

            for (int i = 100; i < trainCandles5m.Count - 20; i++)
            {
                var f = FeatureBuilder.Build(trainCandles5m, i, candles1h, candles1d);
                if (f == null) continue;

                decimal labelValue = LabelBuilder.Build(trainCandles5m, i);
                f.Label = labelValue.ToString();
                dataset.Add(f);
            }

            Console.WriteLine($"📦 Dataset: {dataset.Count} mẫu");

            if (dataset.Count < 100)
            {
                Console.WriteLine("❌ Quá ít mẫu để train. Kiểm tra lại dữ liệu.");
                return;
            }

            Train("model.zip", dataset);
            Console.WriteLine("✅ TRAIN DONE");

            await Task.CompletedTask;
        }
    }
}