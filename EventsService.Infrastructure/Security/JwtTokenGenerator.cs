using EventsService.Application.Interfaces.Auth;
using EventsService.Domain.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EventsService.Infrastructure.Security
{
    public sealed class JwtTokenGenerator(IOptions<JwtOptions> jwtOptions) : IJwtTokenGenerator
    {
        public string Generate(User user)
        {
            var options = jwtOptions.Value;
            var key = Encoding.UTF8.GetBytes(options.Secret);
            if (key.Length < 32)
                throw new InvalidOperationException("Jwt:Secret must contain at least 32 UTF-8 bytes.");

            if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience) || options.LifetimeMinutes <= 0)
                throw new InvalidOperationException("Jwt issuer, audience and a positive lifetime are required.");

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.Login),
                    new Claim("role", user.Role.ToString())
                ],
                notBefore: now,
                expires: now.AddMinutes(options.LifetimeMinutes),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
