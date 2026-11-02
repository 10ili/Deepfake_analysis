using Deepfake_analysis.Auth;
using Deepfake_analysis.Dtos;
using Deepfake_analysis.Services;
using Microsoft.AspNetCore.Mvc;

namespace Deepfake_analysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IUserService users, TokenService tokens) : ControllerBase
    {
        /// <summary>Проверяет логин и пароль и выдаёт токен доступа.</summary>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var user = await users.AuthenticateAsync(request.Login, request.Password);
            if (user == null)
                return Problem("Неверный логин или пароль.", statusCode: StatusCodes.Status401Unauthorized);

            var expires = tokens.GetExpiration();
            return new LoginResponse(tokens.CreateToken(user, expires), expires, user.Login, user.Role);
        }
    }
}
