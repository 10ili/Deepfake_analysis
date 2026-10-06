namespace Deepfake_analysis.Models
{
    /// <summary>Модель-классификатор, выносящая вердикт по изображению.</summary>
    public class Detector : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;

        /// <summary>Порог вероятности класса «Real»: при значении не выше порога изображение считается сгенерированным.</summary>
        public double Threshold { get; private set; } = 0.5;

        public bool IsActive { get; private set; } = true;

        public void SetThreshold(double threshold)
        {
            if (threshold is <= 0 or >= 1)
                throw new ArgumentOutOfRangeException(nameof(threshold), "Порог должен лежать в интервале (0; 1).");

            Threshold = threshold;
        }

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        public Verdict Classify(double realProbability) =>
            realProbability <= Threshold ? Verdict.Generated : Verdict.Real;
    }
}
