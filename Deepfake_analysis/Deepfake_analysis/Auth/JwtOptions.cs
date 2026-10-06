namespace Deepfake_analysis.Auth
{
    /// <summary>Настройки токена из секции "Jwt" файла appsettings.json.</summary>
    public class JwtOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int LifetimeMinutes { get; set; } = 60;
    }
}
