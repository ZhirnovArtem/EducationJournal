using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EducationServer.Domain;
using EducationServer.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EducationServer.Services;

public sealed class JwtProvider : IJwtProvider
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;

    public int AccessTokenMinutes { get; }
    public int RefreshTokenDays { get; }

    public JwtProvider(IConfiguration cfg)
    {
        _issuer = cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing");
        _audience = cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing");
        var secret = cfg["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey missing");
        if (secret.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 chars");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        AccessTokenMinutes = int.TryParse(cfg["Jwt:AccessTokenMinutes"], out var m) ? m : 15;
        RefreshTokenDays = int.TryParse(cfg["Jwt:RefreshTokenDays"], out var d) ? d : 30;
    }

    public string CreateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("login", user.Login),
            new Claim(ClaimTypes.Role, user.RoleCode),
        };

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}