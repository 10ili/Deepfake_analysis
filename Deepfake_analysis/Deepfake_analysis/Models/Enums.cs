namespace Deepfake_analysis.Models
{
    public enum Verdict
    {
        Real,
        Generated
    }

    public enum RiskLevel
    {
        Low,
        High
    }

    /// <summary>Область изображения с наибольшей активацией на карте Integrated Gradients.</summary>
    public enum ImageRegion
    {
        Top,
        Center,
        Bottom,
        Left,
        Right
    }

    public enum AnomalyType
    {
        GeometryAndContours,
        TextureAndNoise
    }
}
