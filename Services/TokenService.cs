using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend_Gestion_Magasin_API.Models;
using Microsoft.Extensions.Configuration;

namespace Backend_Gestion_Magasin_API.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateToken(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? jwtSettings["Secret"]
                ?? throw new InvalidOperationException("JWT_SECRET non configuré.");
            var key = Encoding.ASCII.GetBytes(secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.Nom),
                new Claim("UserId", user.Id)
            };

            // Utiliser uniquement le rôle personnalisé (plus de rôles Identity)
            if (!string.IsNullOrEmpty(user.Role?.NomRole))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role.NomRole));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
