using Deepfake_analysis.Data;
using Deepfake_analysis.Detection;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Background
{
    /// <summary>
    /// Фоновая служба: работает всё время, пока запущен сервер, берёт анализы из очереди и прогоняет
    /// картинку через модель. Анализы считаются строго по одному — нейросеть и так занимает весь процессор.
    /// </summary>
    public sealed class AnalysisWorker(
        AnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ModelRegistry models,
        ILogger<AnalysisWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeueUnfinishedAsync(stoppingToken);

            await foreach (var analysisId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(analysisId);
                }
                catch (Exception ex)
                {
                    // Ошибка одного анализа не должна останавливать всю очередь.
                    logger.LogError(ex, "Не удалось обработать анализ {AnalysisId}", analysisId);
                }
            }
        }

        /// <summary>
        /// После перезапуска сервера очередь в памяти пуста, а в базе могли остаться анализы,
        /// которые ждали или считались в момент выключения. Возвращаем их в очередь.
        /// </summary>
        private async Task RequeueUnfinishedAsync(CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unfinished = await db.Analyses
                .Where(a => a.Status == AnalysisStatus.Queued || a.Status == AnalysisStatus.Running)
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

            foreach (var analysis in unfinished)
                analysis.Requeue();
            await db.SaveChangesAsync(cancellationToken);

            foreach (var analysis in unfinished)
                queue.Enqueue(analysis.Id);

            if (unfinished.Count > 0)
                logger.LogInformation("В очередь возвращено незавершённых анализов: {Count}", unfinished.Count);
        }

        private async Task ProcessAsync(int analysisId)
        {
            // DbContext живёт недолго (на один запрос), поэтому для каждого анализа создаём свой.
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var analysis = await db.Analyses.FindAsync(analysisId);
            if (analysis == null || analysis.Status != AnalysisStatus.Queued)
                return;   // анализ успели удалить, пока он ждал в очереди

            var detector = await db.Detectors.FindAsync(analysis.DetectorId);
            var content = await db.ImageFiles
                .Where(f => f.ImageId == analysis.ImageId)
                .Select(f => f.Content)
                .FirstOrDefaultAsync();
            if (detector == null || content == null)
                return;

            analysis.Start();
            await db.SaveChangesAsync();

            try
            {
                // Расчёт занимает до ~15 секунд процессорного времени — выносим его в отдельный поток.
                var result = await Task.Run(() =>
                {
                    using var rgb = ImageLoader.Decode(content);
                    return models.Get(detector.Model).Analyze(rgb);
                });

                analysis.Complete(detector, result.RealProbability, result.Heatmap);
                if (result.Heatmap != null)
                {
                    db.Heatmaps.Add(new AnalysisHeatmap
                    {
                        AnalysisId = analysis.Id,
                        HeatmapPng = result.Heatmap.HeatmapPng,
                        OverlayPng = result.Heatmap.OverlayPng,
                    });
                }
                logger.LogInformation("Анализ {AnalysisId} готов за {Seconds} с: P(real) = {Probability:F4}",
                    analysis.Id, analysis.DurationSeconds, result.RealProbability);
            }
            catch (Exception ex)
            {
                analysis.Fail(ex.Message);
                logger.LogWarning(ex, "Анализ {AnalysisId} завершился ошибкой", analysis.Id);
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Анализ или картинку удалили, пока шёл расчёт, — сохранять результат некуда.
                logger.LogInformation("Анализ {AnalysisId} удалён во время расчёта, результат отброшен", analysis.Id);
            }
        }
    }
}
