using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace EventsService.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordHasher_ComputesSha256AndVerifiesMatchingPassword()
    {
        var hasher = new Sha256PasswordHasher();
        var hash = hasher.Hash("abc");

        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        Assert.True(hasher.Verify("abc", hash));
        Assert.False(hasher.Verify("abcd", hash));
        Assert.False(hasher.Verify("abc", "!"));
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public void JwtTokenGenerator_SignsTokenWithConfiguredLifetimeAndUserClaims(UserRole role)
    {
        var options = new JwtOptions
        {
            Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Issuer = "test-issuer",
            Audience = "test-audience",
            LifetimeMinutes = 15
        };
        var user = User.Create("test-user", "stored-hash", role);
        var generator = new JwtTokenGenerator(Options.Create(options));
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var principal = handler.ValidateToken(generator.Generate(user), new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            RoleClaimType = "role",
            NameClaimType = JwtRegisteredClaimNames.UniqueName
        }, out var validatedToken);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(user.Login, principal.Identity?.Name);
        Assert.True(principal.IsInRole(role.ToString()));
        Assert.DoesNotContain(principal.Claims, claim => claim.Value == user.PasswordHash);
        Assert.Equal(TimeSpan.FromMinutes(options.LifetimeMinutes), validatedToken.ValidTo - validatedToken.ValidFrom);
    }
}
