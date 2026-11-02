using Deepfake_analysis.Models;
using Microsoft.Extensions.Options;

namespace Deepfake_analysis.Detection
{
    /// <summary>Пути к файлам моделей из секции "Models" файла appsettings.json.</summary>
    public class ModelOptions
    {
        public string EfficientNetB3 { get; set; } = string.Empty;
        public string ResNet50 { get; set; } = string.Empty;

        /// <summary>ResNet50 с весами ImageNet — считает признаки для CatBoost.</summary>
        public string ResNet50Features { get; set; } = string.Empty;

        public string CatBoost { get; set; } = string.Empty;

        /// <summary>Сколько ядер процессора отдавать нейросети; 0 — все.</summary>
        public int Threads { get; set; }
    }

    /// <summary>
    /// Загружает модели с диска и держит их в памяти. Модель грузится при первом обращении:
    /// сервер стартует быстро, а память (~100 МБ на сеть) занимают только используемые модели.
    /// Регистрируется одним экземпляром на всё приложение.
    /// </summary>
    public sealed class ModelRegistry : IDisposable
    {
        private readonly Dictionary<DetectorModel, string[]> _files;
        private readonly Dictionary<DetectorModel, IImageDetector> _loaded = [];
        private readonly Lock _lock = new();
        private readonly ILogger<ModelRegistry> _logger;

        public ModelRegistry(IOptions<ModelOptions> options, IWebHostEnvironment environment, ILogger<ModelRegistry> logger)
        {
            _logger = logger;
            var o = options.Value;

            // Относительные пути считаются от папки приложения.
            string Full(string path) => Path.GetFullPath(path, environment.ContentRootPath);
            _files = new()
            {
                [DetectorModel.EfficientNetB3] = [Full(o.EfficientNetB3)],
                [DetectorModel.ResNet50] = [Full(o.ResNet50)],
                [DetectorModel.ResNet50CatBoost] = [Full(o.ResNet50Features), Full(o.CatBoost)],
            };

            if (o.Threads > 0)
                TorchSharp.torch.set_num_threads(o.Threads);
        }

        /// <summary>Путь к первому ненайденному файлу модели или null, если все файлы на месте.</summary>
        public string? FindMissingFile(DetectorModel model) => _files[model].FirstOrDefault(path => !File.Exists(path));

        public IImageDetector Get(DetectorModel model)
        {
            lock (_lock)
            {
                if (_loaded.TryGetValue(model, out var detector))
                    return detector;

                var missing = FindMissingFile(model);
                if (missing != null)
                    throw new FileNotFoundException($"Не найден файл модели {model}: {missing}", missing);

                var files = _files[model];
                detector = model switch
                {
                    DetectorModel.EfficientNetB3 => NeuralNetworkDetector.EfficientNetB3(files[0]),
                    DetectorModel.ResNet50 => NeuralNetworkDetector.ResNet50(files[0]),
                    _ => new CatBoostDetector(files[0], files[1]),
                };
                _logger.LogInformation("Модель {Model} загружена из {Files}", model, string.Join(", ", files));

                _loaded[model] = detector;
                return detector;
            }
        }

        public void Dispose()
        {
            foreach (var detector in _loaded.Values)
                detector.Dispose();
        }
    }
}
