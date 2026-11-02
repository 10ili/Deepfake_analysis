using Deepfake_analysis.Background;
using Deepfake_analysis.Data;
using Deepfake_analysis.Detection;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Services
{
    public interface IAnalysisService
    {
        Task<List<AnalysisResult>> GetAllAsync(int? imageId, int? detectorId, Verdict? verdict, AnalysisStatus? status);
        Task<AnalysisResult> GetAsync(int id);
        Task<AnalysisResult> QueueAsync(int imageId, int detectorId);
        Task<byte[]> GetHeatmapAsync(int id, bool overlay);
        Task DeleteAsync(int id);
    }

    public class AnalysisService(AppDbContext db, AnalysisQueue queue, ModelRegistry models) : IAnalysisService
    {
        public Task<List<AnalysisResult>> GetAllAsync(int? imageId, int? detectorId, Verdict? verdict, AnalysisStatus? status) =>
            db.Analyses
                .Where(a => imageId == null || a.ImageId == imageId)
                .Where(a => detectorId == null || a.DetectorId == detectorId)
                .Where(a => verdict == null || a.Verdict == verdict)
                .Where(a => status == null || a.Status == status)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

        public async Task<AnalysisResult> GetAsync(int id) =>
            await db.Analyses.FindAsync(id) ?? throw new NotFoundException($"Анализ {id} не найден.");

        /// <summary>
        /// Ставит анализ в очередь и сразу возвращает его в состоянии Queued.
        /// Сам расчёт сделает фоновая служба; результат появится в этой же записи.
        /// </summary>
        public async Task<AnalysisResult> QueueAsync(int imageId, int detectorId)
        {
            var image = await db.Images.FindAsync(imageId)
                        ?? throw new BadRequestException($"Изображение {imageId} не найдено.");
            var detector = await db.Detectors.FindAsync(detectorId)
                           ?? throw new BadRequestException($"Детектор {detectorId} не найден.");

            if (!detector.IsActive)
                throw new ConflictException($"Детектор '{detector.Name}' отключён.");

            if (models.FindMissingFile(detector.Model) is { } missing)
                throw new ServiceUnavailableException($"Модель детектора '{detector.Name}' не найдена на сервере: {missing}");

            var analysis = AnalysisResult.Queue(image, detector);
            db.Analyses.Add(analysis);
            await db.SaveChangesAsync();

            queue.Enqueue(analysis.Id);
            return analysis;
        }

        public async Task<byte[]> GetHeatmapAsync(int id, bool overlay)
        {
            var analysis = await GetAsync(id);
            switch (analysis.Status)
            {
                // Ещё считается: карта будет, но позже — это конфликт состояния (409), а не «нет такой» (404).
                case AnalysisStatus.Queued or AnalysisStatus.Running:
                    throw new ConflictException(
                        $"Анализ ещё не завершён (сейчас: {analysis.Status}). Тепловая карта появится после окончания расчёта.");
                case AnalysisStatus.Failed:
                    throw new NotFoundException($"Анализ завершился ошибкой, тепловой карты нет. Причина: {analysis.Error}");
            }

            if (!analysis.HasHeatmap)
                throw new NotFoundException("Детектор этого анализа не строит тепловую карту.");

            // Select выбирает из базы только одну нужную картинку, а не обе.
            return await db.Heatmaps
                .Where(h => h.AnalysisId == id)
                .Select(h => overlay ? h.OverlayPng : h.HeatmapPng)
                .SingleAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var analysis = await GetAsync(id);

            if (analysis.Status == AnalysisStatus.Running)
                throw new ConflictException("Анализ сейчас выполняется. Дождитесь окончания и удалите его.");

            db.Analyses.Remove(analysis);
            await db.SaveChangesAsync();
        }
    }
}
