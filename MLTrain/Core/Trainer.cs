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
    }
}
