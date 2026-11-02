using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Deepfake_analysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysesController(IAnalysisService analyses) : ControllerBase
    {
        [HttpGet]
        public Task<List<AnalysisResult>> GetAll([FromQuery] int? imageId, [FromQuery] int? detectorId,
            [FromQuery] Verdict? verdict, [FromQuery] AnalysisStatus? status) =>
            analyses.GetAllAsync(imageId, detectorId, verdict, status);

        /// <summary>Анализ по номеру. Пока Status = Queued или Running, результат ещё считается — запросите позже.</summary>
        [HttpGet("{id:int}")]
        public Task<AnalysisResult> GetById(int id) => analyses.GetAsync(id);

        /// <summary>Тепловая карта PNG; с ?overlay=true — карта, наложенная на картинку.</summary>
        [HttpGet("{id:int}/heatmap")]
        public async Task<IActionResult> GetHeatmap(int id, [FromQuery] bool overlay = false) =>
            File(await analyses.GetHeatmapAsync(id, overlay), "image/png");

        /// <summary>
        /// Запускает анализ. Отвечает сразу кодом 202 Accepted («принято в работу»):
        /// расчёт идёт в фоне, а результат появится по адресу из заголовка Location.
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public async Task<ActionResult<AnalysisResult>> Create(AnalysisRequest request)
        {
            var analysis = await analyses.QueueAsync(request.ImageId, request.DetectorId);
            return AcceptedAtAction(nameof(GetById), new { id = analysis.Id }, analysis);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await analyses.DeleteAsync(id);
            return NoContent();
        }
    }
}
