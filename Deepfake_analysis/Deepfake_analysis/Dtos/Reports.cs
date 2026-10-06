using Deepfake_analysis.Models;

namespace Deepfake_analysis.Dtos
{
    public record AnalysisDetails(
        int AnalysisId,
        DateTime CreatedAt,
        int ImageId,
        string FileName,
        int DetectorId,
        string DetectorName,
        double RealProbability,
        Verdict Verdict,
        RiskLevel RiskLevel,
        string Conclusion);

    public record DetectorStatistics(
        int DetectorId,
        string DetectorName,
        bool IsActive,
        double Threshold,
        int AnalysesCount,
        int GeneratedCount,
        int RealCount,
        double? AverageRealProbability);

    public record ImageSummary(
        int ImageId,
        string FileName,
        double Megapixels,
        int AnalysesCount,
        Verdict? LastVerdict,
        string? LastDetectorName,
        DateTime? LastAnalyzedAt);

    public record DetectorVerdict(string DetectorName, double RealProbability, Verdict Verdict);

    public record ImageDisagreement(int ImageId, string FileName, List<DetectorVerdict> Verdicts);
}
