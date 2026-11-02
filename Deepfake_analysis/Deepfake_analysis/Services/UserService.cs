using Deepfake_analysis.Data;
using Deepfake_analysis.Models;
using Microsoft.EntityFrameworkCore;

namespace Deepfake_analysis.Services
{
    public interface IUserService
    {
        /// <summary>Пользователь с таким логином и паролем или null.</summary>
        Task<User?> AuthenticateAsync(string login, string password);
    }

    public class UserService(AppDbContext db) : IUserService
    {
        public async Task<User?> AuthenticateAsync(string login, string password)
        {
            // Логин без учёта регистра: «Admin» и «admin» — один пользователь.
            var user = await db.Users.FirstOrDefaultAsync(u => u.Login.ToLower() == login.ToLower());
            return user != null && user.VerifyPassword(password) ? user : null;
        }
    }
}
