using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Deepfake_analysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController(IImageService images) : ControllerBase
    {
        [HttpGet]
        public Task<List<ImageRecord>> GetAll([FromQuery] string? search, [FromQuery] string? extension) =>
            images.GetAllAsync(search, extension);

        [HttpGet("{id:int}")]
        public Task<ImageRecord> GetById(int id) => images.GetAsync(id);

        /// <summary>Сам файл изображения — откроется в браузере или в Postman как картинка.</summary>
        [HttpGet("{id:int}/file")]
        public async Task<IActionResult> GetFile(int id)
        {
            var (image, content) = await images.GetFileAsync(id);
            return File(content, image.ContentType, image.FileName);
        }

        /// <summary>Загрузка изображения: форма multipart/form-data с полем file (JPEG или PNG до 20 МБ).</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ImageRecord>> Upload(IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            var image = await images.UploadAsync(file.FileName, file.Length, stream);
            return CreatedAtAction(nameof(GetById), new { id = image.Id }, image);
        }

        /// <summary>Переименование изображения (расширение менять нельзя).</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPut("{id:int}")]
        public Task<ImageRecord> Rename(int id, ImageRenameRequest request) => images.RenameAsync(id, request.FileName);

        /// <summary>Удаляет изображение вместе с результатами его анализа.</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await images.DeleteAsync(id);
            return NoContent();
        }
    }
}
