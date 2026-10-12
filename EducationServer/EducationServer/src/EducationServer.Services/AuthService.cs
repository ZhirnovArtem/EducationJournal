using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services.Abstractions;

namespace EducationServer.Services;

public record LoginRequest(string Login, string Password);
public record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
public record MeResponse(int UserId, string Login, string Email, string Role);

public sealed class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwt;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtProvider jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest req, string? ip, string? userAgent, CancellationToken ct)
    {
        var user = await _users.FindByLoginAsync(req.Login, ct);

        if (user is null)
        {
            await _users.InsertLoginAttemptAsync(
                new LoginAttempt(req.Login, false, ip, userAgent), ct);
            throw new BusinessException("Invalid credentials");
        }

        if (!user.IsActive)
            throw new ForbiddenException("Account disabled");

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            throw new ForbiddenException("Account is locked");

        if (!_hasher.Verify(req.Password, user.PasswordHash))
        {
            var attempts = await _users.IncrementFailedAttemptsAsync(user.UserId, ct);
            if (attempts >= MaxFailedAttempts)
                await _users.LockAsync(user.UserId, DateTime.UtcNow.Add(LockDuration), ct);

            await _users.InsertLoginAttemptAsync(
                new LoginAttempt(req.Login, false, ip, userAgent), ct);
            throw new BusinessException("Invalid credentials");
        }

        await _users.ResetFailedAttemptsAsync(user.UserId, ct);
        await _users.UpdateLastLoginAsync(user.UserId, ct);
        await _users.InsertLoginAttemptAsync(
            new LoginAttempt(req.Login, true, ip, userAgent), ct);

        return await IssueTokensAsync(user, userAgent, ct);
    }

    public async Task<LoginResponse> RefreshAsync(
        string refreshToken, string? userAgent, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(refreshToken);
        var row = await _users.FindRefreshTokenAsync(hash, ct)
            ?? throw new BusinessException("Invalid refresh token");

        if (row.RevokedAt is not null)
            throw new BusinessException("Refresh token revoked");
        if (row.ExpiresAt <= DateTime.UtcNow)
            throw new BusinessException("Refresh token expired");

        var user = await _users.FindByIdAsync(row.UserId, ct)
            ?? throw new BusinessException("User not found");

        await _users.RevokeRefreshTokenAsync(hash, ct);
        return await IssueTokensAsync(user, userAgent, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(refreshToken);
        await _users.RevokeRefreshTokenAsync(hash, ct);
    }

    public async Task<MeResponse> MeAsync(int userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        return new MeResponse(user.UserId, user.Login, user.Email, user.RoleCode);
    }

    private async Task<LoginResponse> IssueTokensAsync(
        User user, string? userAgent, CancellationToken ct)
    {
        var access = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();
        var refreshHash = _jwt.HashRefreshToken(refresh);
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);

        await _users.SaveRefreshTokenAsync(user.UserId, refreshHash, expires, userAgent, ct);

        return new LoginResponse(
            access,
            refresh,
            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes));
    }
}