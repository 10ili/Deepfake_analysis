using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Services
{
    public interface IDetectorService
    {
        Task<List<Detector>> GetAllAsync(bool? active);
        Task<Detector> GetAsync(int id);
        Task<Detector> CreateAsync(DetectorRequest request);
        Task<Detector> UpdateAsync(int id, DetectorRequest request);
        Task DeleteAsync(int id);
    }

    public class DetectorService(AppDbContext db) : IDetectorService
    {
        public Task<List<Detector>> GetAllAsync(bool? active) =>
            db.Detectors
                .Where(d => active == null || d.IsActive == active)
                .OrderBy(d => d.Name)
                .ToListAsync();

        public async Task<Detector> GetAsync(int id) =>
            await db.Detectors.FindAsync(id) ?? throw new NotFoundException($"Детектор {id} не найден.");

        public async Task<Detector> CreateAsync(DetectorRequest request)
        {
            var detector = new Detector();
            Apply(detector, request);

            db.Detectors.Add(detector);
            await db.SaveChangesAsync();
            return detector;
        }

        /// <summary>Изменяет детектор; при смене порога вердикты по его анализам пересчитываются.</summary>
        public async Task<Detector> UpdateAsync(int id, DetectorRequest request)
        {
            var detector = await GetAsync(id);
            var analyses = await db.Analyses.Where(a => a.DetectorId == id).ToListAsync();

            // Готовые результаты посчитаны конкретной моделью: если её подменить, они перестанут ей соответствовать.
            if (request.Model!.Value != detector.Model && analyses.Count > 0)
                throw new ConflictException("У детектора есть результаты анализа, модель менять нельзя. Создайте новый детектор.");

            Apply(detector, request);
            foreach (var analysis in analyses)
                analysis.Evaluate(detector);

            await db.SaveChangesAsync();
            return detector;
        }

        public async Task DeleteAsync(int id)
        {
            var detector = await GetAsync(id);

            if (await db.Analyses.AnyAsync(a => a.DetectorId == id))
                throw new ConflictException("У детектора есть результаты анализа. Отключите его вместо удаления.");

            db.Detectors.Remove(detector);
            await db.SaveChangesAsync();
        }

        private static void Apply(Detector detector, DetectorRequest request)
        {
            detector.Name = request.Name.Trim();
            detector.Model = request.Model!.Value;
            detector.SetThreshold(request.Threshold);

            if (request.IsActive)
                detector.Activate();
            else
                detector.Deactivate();
        }
    }
}
