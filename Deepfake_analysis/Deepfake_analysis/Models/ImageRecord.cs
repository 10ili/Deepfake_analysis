namespace Deepfake_analysis.Models
{
    /// <summary>Изображение, загруженное на проверку.</summary>
    public class ImageRecord : IEntity
    {
        public const long MaxSizeBytes = 20 * 1024 * 1024;

        private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png"];

        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

        public double Megapixels => Math.Round(Width * (double)Height / 1_000_000, 2);

        public bool HasSupportedFormat() => SupportedExtensions.Contains(Extension);

        /// <summary>Проверяет, можно ли принять изображение на анализ.</summary>
        public bool CanBeAnalyzed(out string? reason)
        {
            if (!HasSupportedFormat())
            {
                reason = $"Формат '{Extension}' не поддерживается. Допустимые: {string.Join(", ", SupportedExtensions)}.";
                return false;
            }

            if (SizeBytes > MaxSizeBytes)
            {
                reason = $"Размер файла превышает {MaxSizeBytes / (1024 * 1024)} МБ.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
