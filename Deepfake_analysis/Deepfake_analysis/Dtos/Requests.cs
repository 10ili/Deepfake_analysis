using System.ComponentModel.DataAnnotations;
using Deepfake_analysis.Models;

namespace Deepfake_analysis.Dtos
{
    public class ImageRequest
    {
        [Required, StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Range(1, long.MaxValue)]
        public long SizeBytes { get; set; }

        [Range(1, 20000)]
        public int Width { get; set; }

        [Range(1, 20000)]
        public int Height { get; set; }
    }

    public class DetectorRequest
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Architecture { get; set; } = string.Empty;

        [Range(0.01, 0.99)]
        public double Threshold { get; set; } = 0.5;

        public bool IsActive { get; set; } = true;
    }

    public class AnalysisRequest
    {
        [Range(1, int.MaxValue)]
        public int ImageId { get; set; }

        [Range(1, int.MaxValue)]
        public int DetectorId { get; set; }

        [Range(0.0, 1.0)]
        public double RealProbability { get; set; }

        [Range(0.0, double.MaxValue)]
        public double Entropy { get; set; }

        [Range(0.0, 1.0)]
        public double Concentration { get; set; }

        [Range(0.0, 1.0)]
        public double StdDeviation { get; set; }

        [EnumDataType(typeof(ImageRegion))]
        public ImageRegion CriticalRegion { get; set; }

        [EnumDataType(typeof(AnomalyType))]
        public AnomalyType AnomalyType { get; set; }
    }
}
