using System.Security.Claims;
using System.Text;
using Deepfake_analysis.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Deepfake_analysis.Auth
{
    public class TokenService
    {
        private readonly JwtOptions _options;

        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public DateTime GetExpiration() => DateTime.UtcNow.AddMinutes(_options.LifetimeMinutes);

        /// <summary>Выпускает подписанный JWT с логином и ролью пользователя.</summary>
        public string CreateToken(User user, DateTime expires)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, user.Login),
                    new Claim(ClaimTypes.Role, user.Role)
                ]),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Expires = expires,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
