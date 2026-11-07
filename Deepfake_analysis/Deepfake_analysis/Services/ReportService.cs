using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Services
{
    public interface IReportService
    {
        Task<List<AnalysisDetails>> GetAnalysesAsync(Verdict? verdict);
        Task<List<DetectorStatistics>> GetDetectorStatisticsAsync();
        Task<List<ImageSummary>> GetImageSummariesAsync(bool onlyUnchecked);
        Task<List<ImageDisagreement>> GetDisagreementsAsync();
    }

    /// <summary>
    /// Сводные отчёты, объединяющие изображения, детекторы и результаты анализа.
    /// В отчёты попадают только завершённые анализы: у остальных ещё нет вердикта.
    /// </summary>
    public class ReportService(AppDbContext db) : IReportService
    {
        /// <summary>Журнал проверок: каждый анализ с именем файла, детектором и заключением.</summary>
        public async Task<List<AnalysisDetails>> GetAnalysesAsync(Verdict? verdict)
        {
            // Join выполняет база (SQL JOIN), из таблицы картинок берётся только имя файла.
            var rows = await Completed()
                .Where(a => verdict == null || a.Verdict == verdict)
                .Join(db.Images, a => a.ImageId, i => i.Id, (a, i) => new { Analysis = a, i.FileName })
                .Join(db.Detectors, x => x.Analysis.DetectorId, d => d.Id,
                    (x, d) => new { x.Analysis, x.FileName, DetectorName = d.Name })
                .OrderByDescending(x => x.Analysis.CreatedAt)
                .ToListAsync();

            // Текст заключения собирает метод C#, базе он не по силам, поэтому — уже в памяти.
            return rows
                .Select(x => new AnalysisDetails(
                    x.Analysis.Id,
                    x.Analysis.CreatedAt,
                    x.Analysis.ImageId,
                    x.FileName,
                    x.Analysis.DetectorId,
                    x.DetectorName,
                    x.Analysis.RealProbability!.Value,
                    x.Analysis.Verdict!.Value,
                    x.Analysis.BuildConclusion(x.DetectorName)!))
                .ToList();
        }

        /// <summary>
        /// Сравнение детекторов: сколько проверок, какие вердикты, средняя вероятность и время расчёта.
        /// В отчёт попадают и детекторы, которые ещё не использовались.
        /// </summary>
        public async Task<List<DetectorStatistics>> GetDetectorStatisticsAsync()
        {
            // GroupJoin («каждому детектору — его анализы») EF Core в SQL не переводит,
            // поэтому обе таблицы читаются в память и соединяются там.
            var detectors = await db.Detectors.ToListAsync();
            var analyses = await Completed().ToListAsync();

            return detectors
                .GroupJoin(analyses, d => d.Id, a => a.DetectorId, (d, group) => new DetectorStatistics(
                    d.Id,
                    d.Name,
                    d.Model,
                    d.IsActive,
                    d.Threshold,
                    group.Count(),
                    group.Count(a => a.Verdict == Verdict.Generated),
                    group.Count(a => a.Verdict == Verdict.Real),
                    Round(group.Average(a => a.RealProbability), 4),
                    Round(group.Average(a => a.DurationSeconds), 1)))
                .OrderByDescending(s => s.AnalysesCount)
                .ThenBy(s => s.DetectorName)
                .ToList();
        }

        /// <summary>Сводка по изображениям: число проверок и последний вынесенный вердикт.</summary>
        public async Task<List<ImageSummary>> GetImageSummariesAsync(bool onlyUnchecked)
        {
            var images = await db.Images.OrderByDescending(i => i.UploadedAt).ToListAsync();
            var analyses = await Completed().ToListAsync();
            var detectorNames = await db.Detectors.ToDictionaryAsync(d => d.Id, d => d.Name);

            return images
                .GroupJoin(analyses, i => i.Id, a => a.ImageId, (i, group) => new
                {
                    Image = i,
                    Count = group.Count(),
                    Last = group.OrderByDescending(a => a.CompletedAt).FirstOrDefault()
                })
                .Where(x => !onlyUnchecked || x.Count == 0)
                .Select(x => new ImageSummary(
                    x.Image.Id,
                    x.Image.FileName,
                    x.Image.Megapixels,
                    x.Count,
                    x.Last?.Verdict,
                    x.Last == null ? null : detectorNames[x.Last.DetectorId],
                    x.Last?.CompletedAt))
                .ToList();
        }

        /// <summary>Изображения, по которым детекторы разошлись во мнениях.</summary>
        public async Task<List<ImageDisagreement>> GetDisagreementsAsync()
        {
            // GroupBy выполняет база: номера картинок, у которых среди вердиктов больше одного разного.
            var imageIds = await Completed()
                .GroupBy(a => a.ImageId)
                .Where(g => g.Select(a => a.Verdict).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            var rows = await Completed()
                .Where(a => imageIds.Contains(a.ImageId))
                .Join(db.Images, a => a.ImageId, i => i.Id, (a, i) => new { a.ImageId, i.FileName, a.DetectorId, a.RealProbability, a.Verdict })
                .Join(db.Detectors, x => x.DetectorId, d => d.Id, (x, d) => new
                {
                    x.ImageId,
                    x.FileName,
                    Verdict = new DetectorVerdict(d.Name, x.RealProbability!.Value, x.Verdict!.Value)
                })
                .ToListAsync();

            return rows
                .GroupBy(x => new { x.ImageId, x.FileName })
                .Select(g => new ImageDisagreement(g.Key.ImageId, g.Key.FileName, g.Select(x => x.Verdict).ToList()))
                .OrderBy(d => d.ImageId)
                .ToList();
        }

        private IQueryable<AnalysisResult> Completed() => db.Analyses.Where(a => a.Status == AnalysisStatus.Completed);

        private static double? Round(double? value, int digits) => value == null ? null : Math.Round(value.Value, digits);
    }
}
