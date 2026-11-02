namespace Deepfake_analysis.Models
{
    /// <summary>Изображение, загруженное на проверку. Сами байты файла хранятся отдельно — в <see cref="ImageFile"/>.</summary>
    public class ImageRecord
    {
        public const long MaxSizeBytes = 20 * 1024 * 1024;

        /// <summary>
        /// Минимальная ширина и высота в пикселях. У совсем маленькой картинки метрики карты теряют смысл:
        /// зона в треть картинки — это пара пикселей, а «1% самых ярких пикселей» — ни одного.
        /// </summary>
        public const int MinSidePixels = 32;

        private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png"];

        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

        public string ContentType => Extension == ".png" ? "image/png" : "image/jpeg";

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

        /// <summary>Проверяет размеры картинки; они известны только после того, как файл прочитан.</summary>
        public bool HasEnoughResolution(out string? reason)
        {
            var enough = Width >= MinSidePixels && Height >= MinSidePixels;
            reason = enough
                ? null
                : $"Картинка {Width}x{Height} слишком маленькая: ширина и высота должны быть не меньше {MinSidePixels} пикселей.";
            return enough;
        }

        /// <summary>
        /// Новое имя файла допустимо, только если расширение не меняется:
        /// иначе имя «.png» досталось бы содержимому JPEG.
        /// </summary>
        public bool CanBeRenamedTo(string fileName, out string? reason)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var sameFormat = extension == Extension ||
                             (extension is ".jpg" or ".jpeg" && Extension is ".jpg" or ".jpeg");
            reason = sameFormat ? null : $"Расширение файла менять нельзя: было '{Extension}', указано '{extension}'.";
            return sameFormat;
        }
    }

    /// <summary>Содержимое файла изображения. Отдельная таблица, чтобы списки картинок не тянули из базы мегабайты.</summary>
    public class ImageFile
    {
        public int ImageId { get; set; }
        public byte[] Content { get; set; } = [];
    }
}
