using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    /// <summary>Сводные отчёты, объединяющие изображения, детекторы и результаты анализа.</summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly DataStore _store;

        public ReportsController(DataStore store)
        {
            _store = store;
        }

        /// <summary>Журнал проверок: каждый анализ с именем файла, детектором и заключением.</summary>
        [HttpGet("analyses")]
        public IEnumerable<AnalysisDetails> GetAnalyses([FromQuery] Verdict? verdict)
        {
            var images = _store.Images.GetAll();
            var detectors = _store.Detectors.GetAll();

            return _store.Analyses.GetAll()
                .Where(a => verdict == null || a.Verdict == verdict)
                .Join(images, a => a.ImageId, i => i.Id, (a, i) => new { Analysis = a, Image = i })
                .Join(detectors, x => x.Analysis.DetectorId, d => d.Id, (x, d) => new AnalysisDetails(
                    x.Analysis.Id,
                    x.Analysis.CreatedAt,
                    x.Image.Id,
                    x.Image.FileName,
                    d.Id,
                    d.Name,
                    x.Analysis.RealProbability,
                    x.Analysis.Verdict,
                    x.Analysis.RiskLevel,
                    x.Analysis.BuildConclusion(d.Name)))
                .OrderByDescending(r => r.CreatedAt);
        }

        /// <summary>Статистика по каждому детектору, включая те, что ещё не использовались.</summary>
        [HttpGet("detectors")]
        public IEnumerable<DetectorStatistics> GetDetectorStatistics()
        {
            var analyses = _store.Analyses.GetAll();

            return _store.Detectors.GetAll()
                .GroupJoin(analyses, d => d.Id, a => a.DetectorId, (d, group) => new DetectorStatistics(
                    d.Id,
                    d.Name,
                    d.IsActive,
                    d.Threshold,
                    group.Count(),
                    group.Count(a => a.Verdict == Verdict.Generated),
                    group.Count(a => a.Verdict == Verdict.Real),
                    group.Any() ? Math.Round(group.Average(a => a.RealProbability), 4) : null))
                .OrderByDescending(s => s.AnalysesCount);
        }

        /// <summary>Сводка по изображениям: число проверок и последний вынесенный вердикт.</summary>
        [HttpGet("images")]
        public IEnumerable<ImageSummary> GetImageSummaries([FromQuery] bool onlyUnchecked = false)
        {
            var analyses = _store.Analyses.GetAll();
            var detectors = _store.Detectors.GetAll();

            return _store.Images.GetAll()
                .GroupJoin(analyses, i => i.Id, a => a.ImageId, (i, group) => new
                {
                    Image = i,
                    Count = group.Count(),
                    Last = group.OrderByDescending(a => a.CreatedAt).FirstOrDefault()
                })
                .Where(x => !onlyUnchecked || x.Count == 0)
                .Select(x => new ImageSummary(
                    x.Image.Id,
                    x.Image.FileName,
                    x.Image.Megapixels,
                    x.Count,
                    x.Last?.Verdict,
                    x.Last == null ? null : detectors.FirstOrDefault(d => d.Id == x.Last.DetectorId)?.Name,
                    x.Last?.CreatedAt));
        }

        /// <summary>Изображения, по которым детекторы разошлись во мнениях.</summary>
        [HttpGet("disagreements")]
        public IEnumerable<ImageDisagreement> GetDisagreements()
        {
            var images = _store.Images.GetAll();
            var detectors = _store.Detectors.GetAll();

            return _store.Analyses.GetAll()
                .GroupBy(a => a.ImageId)
                .Where(g => g.Select(a => a.Verdict).Distinct().Count() > 1)
                .Join(images, g => g.Key, i => i.Id, (g, i) => new ImageDisagreement(
                    i.Id,
                    i.FileName,
                    g.Join(detectors, a => a.DetectorId, d => d.Id,
                            (a, d) => new DetectorVerdict(d.Name, a.RealProbability, a.Verdict))
                        .ToList()));
        }
    }
}
