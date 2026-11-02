using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Data
{
    /// <summary>Создаёт базу при первом запуске и заполняет её начальными данными.</summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(AppDbContext db)
        {
            // Если файла базы нет — создаёт его и все таблицы по моделям; если есть — ничего не делает.
            await db.Database.EnsureCreatedAsync();

            if (await db.Users.AnyAsync())
                return;

            db.Users.AddRange(CreateUser("admin", "admin123", Roles.Admin), CreateUser("user", "user123", Roles.User));
            db.Detectors.AddRange(
                CreateDetector("EfficientNet-B3", DetectorModel.EfficientNetB3),
                CreateDetector("ResNet50", DetectorModel.ResNet50),
                CreateDetector("ResNet50 + CatBoost", DetectorModel.ResNet50CatBoost));

            await db.SaveChangesAsync();
        }

        private static User CreateUser(string login, string password, string role)
        {
            var user = new User { Login = login, Role = role };
            user.SetPassword(password);
            return user;
        }

        private static Detector CreateDetector(string name, DetectorModel model)
        {
            var detector = new Detector { Name = name, Model = model };
            detector.SetThreshold(0.5);
            return detector;
        }
    }
}
