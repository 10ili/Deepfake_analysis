using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Deepfake_analysis.Models
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }

    /// <summary>Учётная запись пользователя API.</summary>
    public class User : IEntity
    {
        private static readonly PasswordHasher<User> Hasher = new();

        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Role { get; set; } = Roles.User;

        [JsonIgnore]
        public string PasswordHash { get; private set; } = string.Empty;

        public bool IsAdmin => Role == Roles.Admin;

        /// <summary>Сохраняет не сам пароль, а его хеш с солью.</summary>
        public void SetPassword(string password)
        {
            PasswordHash = Hasher.HashPassword(this, password);
        }

        public bool VerifyPassword(string password) =>
            Hasher.VerifyHashedPassword(this, PasswordHash, password) != PasswordVerificationResult.Failed;
    }
}
