using System.ComponentModel.DataAnnotations;
using Deepfake_analysis.Models;

namespace Deepfake_analysis.Dtos
{
    public class ImageRenameRequest
    {
        [Required, StringLength(255)]
        public string FileName { get; set; } = string.Empty;
    }

    public class DetectorRequest
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Обязательное поле: без него модель молча стала бы первой из списка.</summary>
        [Required, EnumDataType(typeof(DetectorModel))]
        public DetectorModel? Model { get; set; }

        [Range(0.01, 0.99)]
        public double Threshold { get; set; } = 0.5;

        public bool IsActive { get; set; } = true;
    }

    /// <summary>Запуск анализа: какую картинку каким детектором проверить. Всё остальное посчитает модель.</summary>
    public class AnalysisRequest
    {
        [Range(1, int.MaxValue)]
        public int ImageId { get; set; }

        [Range(1, int.MaxValue)]
        public int DetectorId { get; set; }
    }
}
