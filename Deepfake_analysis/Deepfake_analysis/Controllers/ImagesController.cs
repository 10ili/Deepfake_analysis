using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly DataStore _store;

        public ImagesController(DataStore store)
        {
            _store = store;
        }

        [HttpGet]
        public IEnumerable<ImageRecord> GetAll([FromQuery] string? search, [FromQuery] string? extension)
        {
            return _store.Images.GetAll()
                .Where(i => search == null || i.FileName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(i => extension == null || i.Extension == "." + extension.TrimStart('.').ToLowerInvariant())
                .OrderByDescending(i => i.UploadedAt);
        }

        [HttpGet("{id:int}")]
        public ActionResult<ImageRecord> GetById(int id)
        {
            var image = _store.Images.Find(id);
            return image == null ? NotFound() : image;
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult<ImageRecord> Create(ImageRequest request)
        {
            var image = new ImageRecord();
            Apply(image, request);

            if (!image.CanBeAnalyzed(out var reason))
                return Problem(reason, statusCode: StatusCodes.Status400BadRequest);

            _store.Images.Add(image);
            return CreatedAtAction(nameof(GetById), new { id = image.Id }, image);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPut("{id:int}")]
        public ActionResult<ImageRecord> Update(int id, ImageRequest request)
        {
            var image = _store.Images.Find(id);
            if (image == null)
                return NotFound();

            var candidate = new ImageRecord();
            Apply(candidate, request);
            if (!candidate.CanBeAnalyzed(out var reason))
                return Problem(reason, statusCode: StatusCodes.Status400BadRequest);

            Apply(image, request);
            return image;
        }

        /// <summary>Удаляет изображение вместе с результатами его анализа.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            if (_store.Images.RemoveAll(i => i.Id == id) == 0)
                return NotFound();

            _store.Analyses.RemoveAll(a => a.ImageId == id);
            return NoContent();
        }

        private static void Apply(ImageRecord image, ImageRequest request)
        {
            image.FileName = request.FileName.Trim();
            image.SizeBytes = request.SizeBytes;
            image.Width = request.Width;
            image.Height = request.Height;
        }
    }
}
