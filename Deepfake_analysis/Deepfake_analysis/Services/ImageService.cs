using Deepfake_analysis.Data;
using Deepfake_analysis.Detection;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Services
{
    public interface IImageService
    {
        Task<List<ImageRecord>> GetAllAsync(string? search, string? extension);
        Task<ImageRecord> GetAsync(int id);
        Task<(ImageRecord Image, byte[] Content)> GetFileAsync(int id);
        Task<ImageRecord> UploadAsync(string fileName, long sizeBytes, Stream content);
        Task<ImageRecord> RenameAsync(int id, string fileName);
        Task DeleteAsync(int id);
    }

    public class ImageService(AppDbContext db) : IImageService
    {
        public Task<List<ImageRecord>> GetAllAsync(string? search, string? extension)
        {
            var ending = extension == null ? null : "." + extension.TrimStart('.').ToLower();

            // Where и OrderBy здесь не выполняются в памяти: EF Core переводит их в SQL-запрос.
            return db.Images
                .Where(i => search == null || i.FileName.ToLower().Contains(search.ToLower()))
                .Where(i => ending == null || i.FileName.ToLower().EndsWith(ending))
                .OrderByDescending(i => i.UploadedAt)
                .ToListAsync();
        }

        public async Task<ImageRecord> GetAsync(int id) =>
            await db.Images.FindAsync(id) ?? throw new NotFoundException($"Изображение {id} не найдено.");

        public async Task<(ImageRecord Image, byte[] Content)> GetFileAsync(int id)
        {
            var image = await GetAsync(id);
            var content = await db.ImageFiles.Where(f => f.ImageId == id).Select(f => f.Content).SingleAsync();
            return (image, content);
        }

        /// <summary>Проверяет файл и сохраняет его в базу вместе с описанием.</summary>
        public async Task<ImageRecord> UploadAsync(string fileName, long sizeBytes, Stream content)
        {
            var image = new ImageRecord { FileName = Path.GetFileName(fileName.Trim()), SizeBytes = sizeBytes };
            if (!image.CanBeAnalyzed(out var reason))
                throw new BadRequestException(reason!);

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            var bytes = buffer.ToArray();

            // Расширение могут подделать, поэтому файл пробуем прочитать как картинку — заодно узнаём размеры.
            try
            {
                using var rgb = ImageLoader.Decode(bytes);
                image.Width = rgb.Width;
                image.Height = rgb.Height;
            }
            catch (InvalidImageException ex)
            {
                throw new BadRequestException(ex.Message);
            }

            // Две записи (описание и содержимое) сохраняются в одной транзакции: либо обе, либо ни одной.
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Images.Add(image);
            await db.SaveChangesAsync();   // после сохранения у картинки появляется Id
            db.ImageFiles.Add(new ImageFile { ImageId = image.Id, Content = bytes });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return image;
        }

        public async Task<ImageRecord> RenameAsync(int id, string fileName)
        {
            var image = await GetAsync(id);
            fileName = Path.GetFileName(fileName.Trim());

            if (!image.CanBeRenamedTo(fileName, out var reason))
                throw new BadRequestException(reason!);

            image.FileName = fileName;
            await db.SaveChangesAsync();
            return image;
        }

        /// <summary>Удаляет изображение; файл, анализы и тепловые карты база удаляет сама (каскадное удаление).</summary>
        public async Task DeleteAsync(int id)
        {
            if (await db.Images.Where(i => i.Id == id).ExecuteDeleteAsync() == 0)
                throw new NotFoundException($"Изображение {id} не найдено.");
        }
    }
}
