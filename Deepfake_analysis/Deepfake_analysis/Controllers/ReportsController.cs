using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Deepfake_analysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    /// <summary>Сводные отчёты, объединяющие изображения, детекторы и результаты анализа.</summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController(IReportService reports) : ControllerBase
    {
        /// <summary>Журнал проверок: каждый анализ с именем файла, детектором и заключением.</summary>
        [HttpGet("analyses")]
        public Task<List<AnalysisDetails>> GetAnalyses([FromQuery] Verdict? verdict) => reports.GetAnalysesAsync(verdict);

        /// <summary>Сравнение детекторов, включая те, что ещё не использовались.</summary>
        [HttpGet("detectors")]
        public Task<List<DetectorStatistics>> GetDetectorStatistics() => reports.GetDetectorStatisticsAsync();

        /// <summary>Сводка по изображениям: число проверок и последний вынесенный вердикт.</summary>
        [HttpGet("images")]
        public Task<List<ImageSummary>> GetImageSummaries([FromQuery] bool onlyUnchecked = false) =>
            reports.GetImageSummariesAsync(onlyUnchecked);

        /// <summary>Изображения, по которым детекторы разошлись во мнениях.</summary>
        [HttpGet("disagreements")]
        public Task<List<ImageDisagreement>> GetDisagreements() => reports.GetDisagreementsAsync();
    }
}
