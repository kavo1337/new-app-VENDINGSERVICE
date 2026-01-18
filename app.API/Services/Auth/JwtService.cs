using app.API.Data.Entity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace app.API.Services.Auth
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateToken(UserAccount user)
        {
            var issuerValue = _configuration["Jwt.Issuer"] ?? "app";
            var audienceValue = _configuration["Jwt.Audience"] ?? "app";
            var keyValue = _configuration["Jwt.Key"] ?? "ab657a7a546ab5aarta565a7567aba12345678901234567890";
            var expiresMinutes = GetAccessTokenMinutes();

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserAccountId.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email.ToString()),
                new(ClaimTypes.NameIdentifier, user.UserAccountId.ToString()),
                new(ClaimTypes.Name, BuilFullName(user)),
                new(ClaimTypes.Role, user.UserRole.Name ?? string.Empty)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuerValue,
                audience: audienceValue,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public DateTime GetRefreshTokenExpiry()
        {
            var days = 7;
            if (int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var parsed))
            {
                days = parsed;
            }

            return DateTime.UtcNow.AddDays(days);
        }

        private int GetAccessTokenMinutes()
        {
            if (int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var minutes))
            {
                return minutes;
            }

            return 60;
        }
        public static string BuilFullName(UserAccount user)
        {
            var parts = new[] { user.FirstName, user.LastName, user.Patronymic };
            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
