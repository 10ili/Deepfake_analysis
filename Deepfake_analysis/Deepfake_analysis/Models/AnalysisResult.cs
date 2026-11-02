using System.Globalization;
using Deepfake_analysis.Detection;

namespace Deepfake_analysis.Models
{
    /// <summary>
    /// Проверка одного изображения одним детектором.
    /// Жизненный цикл: Queued (в очереди) → Running (считается) → Completed или Failed.
    /// Пока анализ не завершён, вероятность, метрики и вердикт пустые (null).
    /// </summary>
    public class AnalysisResult
    {
        public int Id { get; set; }
        public int ImageId { get; private set; }
        public int DetectorId { get; private set; }

        public AnalysisStatus Status { get; private set; } = AnalysisStatus.Queued;

        /// <summary>Причина, если анализ завершился ошибкой.</summary>
        public string? Error { get; private set; }

        /// <summary>Вероятность того, что изображение настоящее (выход модели после сигмоиды).</summary>
        public double? RealProbability { get; private set; }

        // Метрики тепловой карты Integrated Gradients; у детектора без карты (CatBoost) остаются пустыми.
        public bool HasHeatmap { get; private set; }
        public double? Entropy { get; private set; }
        public double? Concentration { get; private set; }
        public double? StdDeviation { get; private set; }
        public ImageRegion? CriticalRegion { get; private set; }
        public AnomalyType? AnomalyType { get; private set; }

        public Verdict? Verdict { get; private set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public double? GeneratedProbability => RealProbability == null ? null : Math.Round(1 - RealProbability.Value, 4);

        public RiskLevel? RiskLevel => Verdict switch
        {
            Models.Verdict.Generated => Models.RiskLevel.High,
            Models.Verdict.Real => Models.RiskLevel.Low,
            _ => null
        };

        /// <summary>Сколько секунд модель считала результат.</summary>
        public double? DurationSeconds => StartedAt == null || CompletedAt == null
            ? null
            : Math.Round((CompletedAt.Value - StartedAt.Value).TotalSeconds, 1);

        /// <summary>Новый анализ в очереди.</summary>
        public static AnalysisResult Queue(ImageRecord image, Detector detector) =>
            new() { ImageId = image.Id, DetectorId = detector.Id };

        public void Start()
        {
            if (Status != AnalysisStatus.Queued)
                throw new InvalidOperationException($"Запустить можно только анализ в очереди, а этот в состоянии {Status}.");

            Status = AnalysisStatus.Running;
            StartedAt = DateTime.UtcNow;
        }

        /// <summary>Записывает ответ модели и выносит вердикт по порогу детектора.</summary>
        public void Complete(Detector detector, double realProbability, HeatmapAnalysis? heatmap)
        {
            EnsureRunning();

            RealProbability = realProbability;
            HasHeatmap = heatmap != null;
            Entropy = heatmap?.Entropy;
            Concentration = heatmap?.Concentration;
            StdDeviation = heatmap?.StdDeviation;
            CriticalRegion = heatmap?.CriticalRegion;
            AnomalyType = heatmap?.AnomalyType;

            Status = AnalysisStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            Evaluate(detector);
        }

        public void Fail(string error)
        {
            EnsureRunning();

            Status = AnalysisStatus.Failed;
            Error = error;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Возвращает в очередь анализ, который не успел досчитаться (например, сервер выключили посреди расчёта).
        /// </summary>
        public void Requeue()
        {
            if (Status != AnalysisStatus.Running)
                return;

            Status = AnalysisStatus.Queued;
            StartedAt = null;
        }

        /// <summary>Выносит вердикт заново — например, когда у детектора поменяли порог.</summary>
        public void Evaluate(Detector detector)
        {
            if (detector.Id != DetectorId)
                throw new ArgumentException("Анализ выполнен другим детектором.", nameof(detector));

            if (RealProbability != null)
                Verdict = detector.Classify(RealProbability.Value);
        }

        /// <summary>Текстовое обоснование вердикта; пока анализ не завершён — null.</summary>
        public string? BuildConclusion(string detectorName)
        {
            if (Status != AnalysisStatus.Completed)
                return null;

            var risk = RiskLevel == Models.RiskLevel.High ? "ВЫСОКИЙ" : "НИЗКИЙ";
            var percent = (GeneratedProbability!.Value * 100).ToString("F2", CultureInfo.InvariantCulture);
            var text = $"Модель {detectorName} определила уровень риска как: {risk} ({percent}%).";

            if (!HasHeatmap)
                return text + " Тепловая карта для этой модели не строится.";

            var region = CriticalRegion switch
            {
                ImageRegion.Top => "в верхней части",
                ImageRegion.Center => "в центральной части",
                ImageRegion.Bottom => "в нижней части",
                ImageRegion.Left => "слева",
                _ => "справа"
            };

            var anomaly = AnomalyType == Models.AnomalyType.GeometryAndContours
                ? "нарушение геометрии и контуров"
                : "неестественная текстура и шум";

            return $"{text} Критическая зона активации обнаружена {region}. " +
                   $"Природа аномалий квалифицирована как: {anomaly}.";
        }

        private void EnsureRunning()
        {
            if (Status != AnalysisStatus.Running)
                throw new InvalidOperationException($"Анализ не выполняется, его состояние: {Status}.");
        }
    }

    /// <summary>Картинки тепловой карты (PNG). Отдельная таблица, чтобы списки анализов не тянули из базы мегабайты.</summary>
    public class AnalysisHeatmap
    {
        public int AnalysisId { get; set; }
        public byte[] HeatmapPng { get; set; } = [];
        public byte[] OverlayPng { get; set; } = [];
    }
}
