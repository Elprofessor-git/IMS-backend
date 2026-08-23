using Backend_Gestion_Magasin_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Services
{
    public class AuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenService _tokenService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            TokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public async Task<bool> ValidateUserAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return false;

            return await _userManager.CheckPasswordAsync(user, password);
        }

        public async Task<string?> GenerateTokenAsync(string email)
        {
            var user = await _userManager.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return null;

            return _tokenService.CreateToken(user);
        }

        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }
    }
}
