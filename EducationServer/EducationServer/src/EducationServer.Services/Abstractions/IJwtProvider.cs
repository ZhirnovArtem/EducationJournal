using EducationServer.Domain;

namespace EducationServer.Services.Abstractions;

public interface IJwtProvider
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
    int AccessTokenMinutes { get; }
    int RefreshTokenDays { get; }
}