using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Services
{
    public interface IJwtTokenGenerator
    {
        (string token, DateTime expiresAt) GenerateToken(User user);
    }

    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IConfiguration _config;

        public JwtTokenGenerator(IConfiguration config)
        {
            _config = config;
        }

        public (string token, DateTime expiresAt) GenerateToken(User user)
        {
            var secretKey = _config["JwtSettings:Secret"] ?? "CNPMNC_SUPER_SECRET_KEY_JWT_2026_STUDENT_ACTIVITY_MANAGEMENT_SYSTEM";
            var issuer = _config["JwtSettings:Issuer"] ?? "StudentActivityManagement.API";
            var audience = _config["JwtSettings:Audience"] ?? "StudentActivityClient";
            var expiryMinutes = double.Parse(_config["JwtSettings:ExpiryMinutes"] ?? "1440"); // Default 24 hours

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("StudentCode", user.StudentCode),
                new Claim("Campus", user.Campus ?? ""),
                new Claim("IsClassMonitor", user.IsClassMonitor.ToString())
            };

            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return (tokenHandler.WriteToken(token), expiresAt);
        }
    }
}
