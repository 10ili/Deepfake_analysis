using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectorsController : ControllerBase
    {
        private readonly DataStore _store;

        public DetectorsController(DataStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IEnumerable<Detector> GetAll([FromQuery] bool? active)
        {
            return _store.Detectors.GetAll()
                .Where(d => active == null || d.IsActive == active)
                .OrderBy(d => d.Name);
        }

        [HttpGet("{id:int}")]
        public ActionResult<Detector> GetById(int id)
        {
            var detector = _store.Detectors.Find(id);
            return detector == null ? NotFound() : detector;
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult<Detector> Create(DetectorRequest request)
        {
            var detector = new Detector();
            Apply(detector, request);

            _store.Detectors.Add(detector);
            return CreatedAtAction(nameof(GetById), new { id = detector.Id }, detector);
        }

        /// <summary>Изменяет детектор; при смене порога вердикты по его анализам пересчитываются.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPut("{id:int}")]
        public ActionResult<Detector> Update(int id, DetectorRequest request)
        {
            var detector = _store.Detectors.Find(id);
            if (detector == null)
                return NotFound();

            Apply(detector, request);

            var analyses = _store.Analyses.GetAll().Where(a => a.DetectorId == id);
            foreach (var analysis in analyses)
                analysis.Evaluate(detector);

            return detector;
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            if (_store.Detectors.Find(id) == null)
                return NotFound();

            if (_store.Analyses.GetAll().Any(a => a.DetectorId == id))
                return Problem("У детектора есть результаты анализа. Отключите его вместо удаления.",
                    statusCode: StatusCodes.Status409Conflict);

            _store.Detectors.RemoveAll(d => d.Id == id);
            return NoContent();
        }

        private static void Apply(Detector detector, DetectorRequest request)
        {
            detector.Name = request.Name.Trim();
            detector.Architecture = request.Architecture.Trim();
            detector.SetThreshold(request.Threshold);

            if (request.IsActive)
                detector.Activate();
            else
                detector.Deactivate();
        }
    }
}
