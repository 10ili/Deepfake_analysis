using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Deepfake_analysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectorsController(IDetectorService detectors) : ControllerBase
    {
        [HttpGet]
        public Task<List<Detector>> GetAll([FromQuery] bool? active) => detectors.GetAllAsync(active);

        [HttpGet("{id:int}")]
        public Task<Detector> GetById(int id) => detectors.GetAsync(id);

        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public async Task<ActionResult<Detector>> Create(DetectorRequest request)
        {
            var detector = await detectors.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = detector.Id }, detector);
        }

        /// <summary>Изменяет детектор; при смене порога вердикты по его анализам пересчитываются.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPut("{id:int}")]
        public Task<Detector> Update(int id, DetectorRequest request) => detectors.UpdateAsync(id, request);

        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await detectors.DeleteAsync(id);
            return NoContent();
        }
    }
}
