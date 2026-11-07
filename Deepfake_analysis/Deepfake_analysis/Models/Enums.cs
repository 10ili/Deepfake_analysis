namespace Deepfake_analysis.Models
{
    public enum Verdict
    {
        Real,
        Generated
    }

    /// <summary>Обученная модель, на которой работает детектор.</summary>
    public enum DetectorModel
    {
        EfficientNetB3,
        ResNet50,
        ResNet50CatBoost
    }

    /// <summary>Этап, на котором находится анализ.</summary>
    public enum AnalysisStatus
    {
        /// <summary>Ждёт своей очереди.</summary>
        Queued,

        /// <summary>Модель прямо сейчас считает результат.</summary>
        Running,

        Completed,
        Failed
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
