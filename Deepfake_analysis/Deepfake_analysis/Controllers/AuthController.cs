using Deepfake_analysis.Auth;
using Deepfake_analysis.Data;
using Deepfake_analysis.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DataStore _store;
        private readonly TokenService _tokens;

        public AuthController(DataStore store, TokenService tokens)
        {
            _store = store;
            _tokens = tokens;
        }

        /// <summary>Проверяет логин и пароль и выдаёт токен доступа.</summary>
        [HttpPost("login")]
        public ActionResult<LoginResponse> Login(LoginRequest request)
        {
            var user = _store.Users.GetAll()
                .FirstOrDefault(u => u.Login.Equals(request.Login, StringComparison.OrdinalIgnoreCase));

            if (user == null || !user.VerifyPassword(request.Password))
                return Problem("Неверный логин или пароль.", statusCode: StatusCodes.Status401Unauthorized);

            var expires = _tokens.GetExpiration();
            return new LoginResponse(_tokens.CreateToken(user, expires), expires, user.Login, user.Role);
        }
    }
}
