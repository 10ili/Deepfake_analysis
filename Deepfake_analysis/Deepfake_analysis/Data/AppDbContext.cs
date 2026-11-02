using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Deepfake_analysis.Data
{
    /// <summary>
    /// Связь с базой SQLite. Каждое свойство DbSet — таблица; запросы LINQ к ним EF Core переводит в SQL.
    /// </summary>
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Detector> Detectors => Set<Detector>();
        public DbSet<ImageRecord> Images => Set<ImageRecord>();
        public DbSet<ImageFile> ImageFiles => Set<ImageFile>();
        public DbSet<AnalysisResult> Analyses => Set<AnalysisResult>();
        public DbSet<AnalysisHeatmap> Heatmaps => Set<AnalysisHeatmap>();

        /// <summary>
        /// Все даты в проекте — UTC. SQLite хранит дату текстом без пометки о поясе, и прочитанная дата
        /// получала бы вид «неизвестно какой пояс» (в JSON без Z). Конвертер возвращает ей пометку UTC.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
            toDatabase => toDatabase,
            fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(user =>
            {
                user.HasIndex(u => u.Login).IsUnique();
                user.Property(u => u.Login).HasMaxLength(50);
                user.Property(u => u.Role).HasMaxLength(20);
            });

            modelBuilder.Entity<Detector>(detector =>
            {
                detector.Property(d => d.Name).HasMaxLength(100);
                // Перечисления хранятся текстом («ResNet50»), а не числом — так базу проще читать глазами.
                detector.Property(d => d.Model).HasConversion<string>().HasMaxLength(30);
            });

            modelBuilder.Entity<ImageRecord>(image =>
            {
                image.ToTable("Images");
                image.Property(i => i.FileName).HasMaxLength(255);
            });

            // Файл картинки: одна строка на картинку, удаляется вместе с ней.
            modelBuilder.Entity<ImageFile>(file =>
            {
                file.HasKey(f => f.ImageId);
                file.HasOne<ImageRecord>().WithOne().HasForeignKey<ImageFile>(f => f.ImageId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnalysisResult>(analysis =>
            {
                analysis.ToTable("Analyses");
                analysis.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
                analysis.Property(a => a.Verdict).HasConversion<string>().HasMaxLength(20);
                analysis.Property(a => a.CriticalRegion).HasConversion<string>().HasMaxLength(20);
                analysis.Property(a => a.AnomalyType).HasConversion<string>().HasMaxLength(30);

                // Удалили картинку — удалились и её анализы.
                analysis.HasOne<ImageRecord>().WithMany().HasForeignKey(a => a.ImageId).OnDelete(DeleteBehavior.Cascade);
                // Детектор с анализами удалить нельзя: база не даст, а сервис заранее ответит 409.
                analysis.HasOne<Detector>().WithMany().HasForeignKey(a => a.DetectorId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AnalysisHeatmap>(heatmap =>
            {
                heatmap.HasKey(h => h.AnalysisId);
                heatmap.HasOne<AnalysisResult>().WithOne().HasForeignKey<AnalysisHeatmap>(h => h.AnalysisId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
