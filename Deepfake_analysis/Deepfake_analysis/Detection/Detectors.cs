using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using TorchSharp;
using static TorchSharp.torch;

namespace Deepfake_analysis.Detection
{
    /// <summary>Ответ детектора по одной картинке.</summary>
    public sealed class DetectionResult
    {
        /// <summary>Вероятность того, что картинка настоящая (класс «Real»).</summary>
        public double RealProbability { get; init; }

        /// <summary>Тепловая карта и её метрики; есть только у нейросетей.</summary>
        public HeatmapAnalysis? Heatmap { get; init; }
    }

    /// <summary>Модель, которая по картинке RGB выдаёт вероятность «Real».</summary>
    public interface IImageDetector : IDisposable
    {
        DetectionResult Analyze(Mat rgb);
    }

    /// <summary>Нейросеть с одним выходом (EfficientNet-B3 или ResNet50): вероятность и тепловая карта.</summary>
    public sealed class NeuralNetworkDetector(string modelPath, int inputSize) : IImageDetector
    {
        private readonly jit.ScriptModule<Tensor, Tensor> _model = TorchScriptModel.Load(modelPath);
        private readonly Lock _lock = new();

        public static NeuralNetworkDetector EfficientNetB3(string modelPath) => new(modelPath, 300);
        public static NeuralNetworkDetector ResNet50(string modelPath) => new(modelPath, 224);

        public DetectionResult Analyze(Mat rgb)
        {
            // Сеть одна на всё приложение, а градиенты считаются внутри неё — по одной картинке за раз.
            lock (_lock)
            {
                using var scope = torch.NewDisposeScope();
                var input = ImagePreprocessing.ToInputTensor(rgb, inputSize);

                double logit;
                using (torch.no_grad())
                    logit = _model.call(input).item<float>();

                var attributions = IntegratedGradients.Attribute(_model, input);
                return new DetectionResult
                {
                    RealProbability = Sigmoid(logit),
                    Heatmap = Heatmap.Analyze(attributions, rgb),
                };
            }
        }

        public void Dispose() => _model.Dispose();

        // Сигмоида превращает ответ сети (любое число) в вероятность от 0 до 1.
        private static double Sigmoid(double logit) => 1f / (1f + MathF.Exp(-(float)logit));
    }

    /// <summary>
    /// ResNet50 + CatBoost: ResNet50 с весами ImageNet описывает картинку 2048 признаками,
    /// по ним решение принимает CatBoost (модель в формате ONNX). Тепловой карты нет.
    /// </summary>
    public sealed class CatBoostDetector(string featuresModelPath, string catBoostModelPath) : IImageDetector
    {
        private const int InputSize = 224;

        private readonly jit.ScriptModule<Tensor, Tensor> _features = TorchScriptModel.Load(featuresModelPath);
        private readonly InferenceSession _catBoost = new(catBoostModelPath);
        private readonly Lock _lock = new();

        public DetectionResult Analyze(Mat rgb) =>
            new() { RealProbability = PredictProbability(ExtractFeatures(rgb)) };

        /// <summary>2048 признаков картинки — выход ResNet50 перед последним слоем.</summary>
        private float[] ExtractFeatures(Mat rgb)
        {
            lock (_lock)
            {
                using var scope = torch.NewDisposeScope();
                using var noGrad = torch.no_grad();
                var input = ImagePreprocessing.ToInputTensor(rgb, InputSize);
                return _features.call(input).data<float>().ToArray();
            }
        }

        /// <summary>Вероятность «Real» по признакам: выход probability_tensor = [сгенерировано, настоящее].</summary>
        private double PredictProbability(float[] features)
        {
            var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(features, [1, features.Length]);
            using var outputs = _catBoost.Run([NamedOnnxValue.CreateFromTensor("features", tensor)], ["probability_tensor"]);
            return outputs[0].AsTensor<float>()[0, 1];
        }

        public void Dispose()
        {
            _features.Dispose();
            _catBoost.Dispose();
        }
    }

    internal static class TorchScriptModel
    {
        /// <summary>
        /// Загружает сеть TorchScript для работы, а не для обучения: режим eval и веса без градиентов.
        /// Без этого каждый обратный проход Integrated Gradients считал бы ещё и градиенты всех весов сети
        /// и копил бы их в памяти. Градиент по самой картинке нужен — он по-прежнему считается.
        /// </summary>
        public static jit.ScriptModule<Tensor, Tensor> Load(string path)
        {
            var model = torch.jit.load<Tensor, Tensor>(path);
            model.eval();
            foreach (var parameter in model.parameters())
                parameter.requires_grad_(false);
            return model;
        }
    }
}
