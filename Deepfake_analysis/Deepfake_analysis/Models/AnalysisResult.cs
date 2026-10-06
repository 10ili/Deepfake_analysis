using System.Globalization;

namespace Deepfake_analysis.Models
{
    /// <summary>Результат проверки одного изображения одним детектором.</summary>
    public class AnalysisResult : IEntity
    {
        public int Id { get; set; }
        public int ImageId { get; set; }
        public int DetectorId { get; private set; }

        /// <summary>Вероятность того, что изображение настоящее (выход модели после сигмоиды).</summary>
        public double RealProbability { get; set; }

        // Метрики карты атрибуций Integrated Gradients
        public double Entropy { get; set; }
        public double Concentration { get; set; }
        public double StdDeviation { get; set; }
        public ImageRegion CriticalRegion { get; set; }
        public AnomalyType AnomalyType { get; set; }

        public Verdict Verdict { get; private set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public double GeneratedProbability => Math.Round(1 - RealProbability, 4);

        public RiskLevel RiskLevel => Verdict == Verdict.Generated ? RiskLevel.High : RiskLevel.Low;

        /// <summary>Привязывает результат к детектору и выносит вердикт по его порогу.</summary>
        public void Evaluate(Detector detector)
        {
            DetectorId = detector.Id;
            Verdict = detector.Classify(RealProbability);
        }

        /// <summary>Текстовое обоснование вердикта.</summary>
        public string BuildConclusion(string detectorName)
        {
            var risk = RiskLevel == RiskLevel.High ? "ВЫСОКИЙ" : "НИЗКИЙ";
            var percent = (GeneratedProbability * 100).ToString("F2", CultureInfo.InvariantCulture);

            var region = CriticalRegion switch
            {
                ImageRegion.Top => "в верхней части",
                ImageRegion.Center => "в центральной части",
                ImageRegion.Bottom => "в нижней части",
                ImageRegion.Left => "слева",
                _ => "справа"
            };

            var anomaly = AnomalyType == AnomalyType.GeometryAndContours
                ? "нарушение геометрии и контуров"
                : "неестественная текстура и шум";

            return $"Модель {detectorName} определила уровень риска как: {risk} ({percent}%). " +
                   $"Критическая зона активации обнаружена {region}. " +
                   $"Природа аномалий квалифицирована как: {anomaly}.";
        }
    }
}
