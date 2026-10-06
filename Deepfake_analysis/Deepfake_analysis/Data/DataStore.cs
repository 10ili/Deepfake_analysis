using Deepfake_analysis.Models;

namespace Deepfake_analysis.Data
{
    /// <summary>Хранилище в памяти с тестовыми данными.</summary>
    public class DataStore
    {
        public InMemoryRepository<ImageRecord> Images { get; } = new();
        public InMemoryRepository<Detector> Detectors { get; } = new();
        public InMemoryRepository<AnalysisResult> Analyses { get; } = new();
        public InMemoryRepository<User> Users { get; } = new();

        public DataStore()
        {
            Seed();
        }

        private void Seed()
        {
            AddUser("admin", "admin123", Roles.Admin);
            AddUser("user", "user123", Roles.User);

            var efficientNet = AddDetector("EfficientNet-B3 (finetuned, epoch 5)", "EfficientNet-B3", 0.5);
            var resNet = AddDetector("ResNet50 (сквозное обучение)", "ResNet50", 0.5);
            var catBoost = AddDetector("ResNet50 + CatBoost", "ResNet50 + CatBoost", 0.45);
            catBoost.Deactivate();

            var portrait = AddImage("portrait_midjourney.png", 2_450_000, 1024, 1024, new DateTime(2026, 9, 28, 10, 15, 0, DateTimeKind.Utc));
            var street = AddImage("street_photo.jpg", 4_120_000, 4000, 3000, new DateTime(2026, 9, 29, 14, 40, 0, DateTimeKind.Utc));
            var landscape = AddImage("landscape_sd.jpeg", 1_380_000, 1536, 1024, new DateTime(2026, 10, 1, 9, 5, 0, DateTimeKind.Utc));
            AddImage("passport_scan.png", 3_300_000, 2480, 3508, new DateTime(2026, 10, 3, 18, 30, 0, DateTimeKind.Utc));

            AddAnalysis(portrait, efficientNet, 0.07, 3.92, 0.118, 0.142, ImageRegion.Center, AnomalyType.TextureAndNoise, 1);
            AddAnalysis(portrait, resNet, 0.21, 4.15, 0.094, 0.127, ImageRegion.Top, AnomalyType.TextureAndNoise, 2);
            AddAnalysis(street, efficientNet, 0.93, 4.78, 0.051, 0.083, ImageRegion.Bottom, AnomalyType.GeometryAndContours, 1);
            AddAnalysis(street, resNet, 0.88, 4.61, 0.057, 0.091, ImageRegion.Left, AnomalyType.GeometryAndContours, 2);
            AddAnalysis(landscape, efficientNet, 0.34, 4.23, 0.087, 0.119, ImageRegion.Top, AnomalyType.TextureAndNoise, 1);
            AddAnalysis(landscape, resNet, 0.62, 4.49, 0.066, 0.102, ImageRegion.Right, AnomalyType.GeometryAndContours, 2);
            AddAnalysis(landscape, catBoost, 0.41, 4.31, 0.079, 0.111, ImageRegion.Top, AnomalyType.TextureAndNoise, 3);
        }

        private void AddUser(string login, string password, string role)
        {
            var user = new User { Login = login, Role = role };
            user.SetPassword(password);
            Users.Add(user);
        }

        private Detector AddDetector(string name, string architecture, double threshold)
        {
            var detector = new Detector { Name = name, Architecture = architecture };
            detector.SetThreshold(threshold);
            return Detectors.Add(detector);
        }

        private ImageRecord AddImage(string fileName, long sizeBytes, int width, int height, DateTime uploadedAt) =>
            Images.Add(new ImageRecord
            {
                FileName = fileName,
                SizeBytes = sizeBytes,
                Width = width,
                Height = height,
                UploadedAt = uploadedAt
            });

        private void AddAnalysis(ImageRecord image, Detector detector, double realProbability, double entropy,
            double concentration, double std, ImageRegion region, AnomalyType anomaly, int minutesAfterUpload)
        {
            var analysis = new AnalysisResult
            {
                ImageId = image.Id,
                RealProbability = realProbability,
                Entropy = entropy,
                Concentration = concentration,
                StdDeviation = std,
                CriticalRegion = region,
                AnomalyType = anomaly,
                CreatedAt = image.UploadedAt.AddMinutes(minutesAfterUpload)
            };
            analysis.Evaluate(detector);
            Analyses.Add(analysis);
        }
    }
}
