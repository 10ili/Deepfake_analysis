using System.ComponentModel.DataAnnotations;

namespace Deepfake_analysis.Dtos
{
    public class LoginRequest
    {
        [Required]
        public string Login { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public record LoginResponse(string Token, DateTime ExpiresAt, string Login, string Role);
}
