using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysesController : ControllerBase
    {
        private readonly DataStore _store;

        public AnalysesController(DataStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IEnumerable<AnalysisResult> GetAll([FromQuery] int? imageId, [FromQuery] int? detectorId,
            [FromQuery] Verdict? verdict)
        {
            return _store.Analyses.GetAll()
                .Where(a => imageId == null || a.ImageId == imageId)
                .Where(a => detectorId == null || a.DetectorId == detectorId)
                .Where(a => verdict == null || a.Verdict == verdict)
                .OrderByDescending(a => a.CreatedAt);
        }

        [HttpGet("{id:int}")]
        public ActionResult<AnalysisResult> GetById(int id)
        {
            var analysis = _store.Analyses.Find(id);
            return analysis == null ? NotFound() : analysis;
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult<AnalysisResult> Create(AnalysisRequest request)
        {
            var error = ResolveDetector(request, out var detector);
            if (error != null)
                return error;

            var analysis = new AnalysisResult();
            Apply(analysis, request, detector!);

            _store.Analyses.Add(analysis);
            return CreatedAtAction(nameof(GetById), new { id = analysis.Id }, analysis);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPut("{id:int}")]
        public ActionResult<AnalysisResult> Update(int id, AnalysisRequest request)
        {
            var analysis = _store.Analyses.Find(id);
            if (analysis == null)
                return NotFound();

            var error = ResolveDetector(request, out var detector);
            if (error != null)
                return error;

            Apply(analysis, request, detector!);
            return analysis;
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            return _store.Analyses.RemoveAll(a => a.Id == id) == 0 ? NotFound() : NoContent();
        }

        private ObjectResult? ResolveDetector(AnalysisRequest request, out Detector? detector)
        {
            detector = _store.Detectors.Find(request.DetectorId);

            if (_store.Images.Find(request.ImageId) == null)
                return Problem($"Изображение {request.ImageId} не найдено.", statusCode: StatusCodes.Status400BadRequest);

            if (detector == null)
                return Problem($"Детектор {request.DetectorId} не найден.", statusCode: StatusCodes.Status400BadRequest);

            if (!detector.IsActive)
                return Problem($"Детектор '{detector.Name}' отключён.", statusCode: StatusCodes.Status409Conflict);

            return null;
        }

        private static void Apply(AnalysisResult analysis, AnalysisRequest request, Detector detector)
        {
            analysis.ImageId = request.ImageId;
            analysis.RealProbability = request.RealProbability;
            analysis.Entropy = request.Entropy;
            analysis.Concentration = request.Concentration;
            analysis.StdDeviation = request.StdDeviation;
            analysis.CriticalRegion = request.CriticalRegion;
            analysis.AnomalyType = request.AnomalyType;
            analysis.Evaluate(detector);
        }
    }
}
