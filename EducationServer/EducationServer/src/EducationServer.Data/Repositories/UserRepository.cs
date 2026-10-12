using System.Data.Common;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;

namespace EducationServer.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;
    public UserRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string UserColumns = @"
        u.UserId, u.Login, u.Email, u.PasswordHash, u.PasswordAlgo, u.RoleId,
        r.Code AS RoleCode, u.IsActive, u.FailedAttempts, u.LockedUntil,
        u.LastLoginAt, u.CreatedAt, u.DeletedAt";

    private static User MapUser(DbDataReader r) => new()
    {
        UserId = r.GetInt32(0),
        Login = r.GetString(1),
        Email = r.GetString(2),
        PasswordHash = (byte[])r[3],
        PasswordAlgo = r.GetString(4),
        RoleId = r.GetInt32(5),
        RoleCode = r.GetString(6),
        IsActive = r.GetBoolean(7),
        FailedAttempts = r.GetInt32(8),
        LockedUntil = r.IsDBNull(9) ? null : r.GetDateTime(9),
        LastLoginAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
        CreatedAt = r.GetDateTime(11),
        DeletedAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
    };
    public async Task<IReadOnlyList<UserListItem>> ListAsync(
    int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
        SELECT u.UserId, u.Login, u.Email, u.RoleId, r.Code, u.IsActive, u.LastLoginAt
        FROM Users u
        JOIN Roles r ON r.RoleId = u.RoleId
        WHERE u.DeletedAt IS NULL
        ORDER BY u.UserId
        OFFSET @offset LIMIT @limit";
        return await conn.QueryAsync(sql, r => new UserListItem(
            r.GetInt32(0), r.GetString(1), r.GetString(2),
            r.GetInt32(3), r.GetString(4), r.GetBoolean(5),
            r.IsDBNull(6) ? null : r.GetDateTime(6)),
            c => c.Bind("@offset", (page - 1) * pageSize)
                  .Bind("@limit", pageSize), ct);
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE DeletedAt IS NULL", null, ct);
    }

    public async Task SoftDeleteAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
        UPDATE Users SET DeletedAt = NOW(), IsActive = FALSE
        WHERE UserId = @id",
            c => c.Bind("@id", userId), ct);
    }
    public async Task<User?> FindByLoginAsync(string login, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $@"
            SELECT {UserColumns}
            FROM Users u
            JOIN Roles r ON r.RoleId = u.RoleId
            WHERE (u.Login = @login OR u.Email = @login) AND u.DeletedAt IS NULL
            LIMIT 1";
        return await conn.QueryFirstAsync(sql, MapUser,
            c => c.Bind("@login", login), ct);
    }

    public async Task<User?> FindByIdAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $@"
            SELECT {UserColumns}
            FROM Users u
            JOIN Roles r ON r.RoleId = u.RoleId
            WHERE u.UserId = @id AND u.DeletedAt IS NULL";
        return await conn.QueryFirstAsync(sql, MapUser,
            c => c.Bind("@id", userId), ct);
    }

    public async Task<int> InsertAsync(User user, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Users (Login, Email, PasswordHash, PasswordAlgo, RoleId, IsActive)
            VALUES (@login, @email, @hash, @algo, @role, TRUE)
            RETURNING UserId";
        return await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@login", user.Login)
            .Bind("@email", user.Email)
            .Bind("@hash", user.PasswordHash)
            .Bind("@algo", user.PasswordAlgo)
            .Bind("@role", user.RoleId), ct);
    }

    public async Task UpdateLastLoginAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Users SET LastLoginAt = NOW() WHERE UserId = @id",
            c => c.Bind("@id", userId), ct);
    }

    public async Task<int> IncrementFailedAttemptsAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(@"
            UPDATE Users SET FailedAttempts = FailedAttempts + 1
            WHERE UserId = @id
            RETURNING FailedAttempts",
            c => c.Bind("@id", userId), ct);
    }

    public async Task ResetFailedAttemptsAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Users SET FailedAttempts = 0, LockedUntil = NULL
            WHERE UserId = @id",
            c => c.Bind("@id", userId), ct);
    }

    public async Task LockAsync(int userId, DateTime until, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Users SET LockedUntil = @until WHERE UserId = @id",
            c => c.Bind("@id", userId).Bind("@until", until), ct);
    }

    public async Task<Role?> GetRoleAsync(int roleId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = "SELECT RoleId, Code, Title FROM Roles WHERE RoleId = @id";
        return await conn.QueryFirstAsync(sql,
            r => new Role
            {
                RoleId = r.GetInt32(0),
                Code = r.GetString(1),
                Title = r.GetString(2)
            },
            c => c.Bind("@id", roleId), ct);
    }

    public async Task InsertLoginAttemptAsync(LoginAttempt attempt, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO LoginAttempts (Login, Success, IpAddress, UserAgent)
            VALUES (@login, @success, @ip, @ua)";
        await conn.ExecuteAsync(sql, c => c
            .Bind("@login", attempt.Login)
            .Bind("@success", attempt.Success)
            .Bind("@ip", attempt.Ip)
            .Bind("@ua", attempt.UserAgent), ct);
    }

    public async Task SaveRefreshTokenAsync(int userId, string tokenHash,
        DateTime expiresAt, string? userAgent, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO RefreshTokens (UserId, TokenHash, ExpiresAt, UserAgent)
            VALUES (@uid, @hash, @exp, @ua)";
        await conn.ExecuteAsync(sql, c => c
            .Bind("@uid", userId)
            .Bind("@hash", tokenHash)
            .Bind("@exp", expiresAt)
            .Bind("@ua", userAgent), ct);
    }

    public async Task<RefreshTokenRow?> FindRefreshTokenAsync(
        string tokenHash, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT RefreshTokenId, UserId, TokenHash, ExpiresAt, RevokedAt
            FROM RefreshTokens WHERE TokenHash = @hash";
        return await conn.QueryFirstAsync(sql, r => new RefreshTokenRow
        {
            RefreshTokenId = r.GetInt64(0),
            UserId = r.GetInt32(1),
            TokenHash = r.GetString(2),
            ExpiresAt = r.GetDateTime(3),
            RevokedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
        }, c => c.Bind("@hash", tokenHash), ct);
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE RefreshTokens SET RevokedAt = NOW() WHERE TokenHash = @hash",
            c => c.Bind("@hash", tokenHash), ct);
    }

    public async Task UpdateAsync(int userId, string? email, int roleId, bool isActive, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
        UPDATE Users
        SET Email = COALESCE(@email, Email),
            RoleId = @roleId,
            IsActive = @isActive,
            UpdatedAt = NOW()
        WHERE UserId = @id",
            c => c.Bind("@id", userId)
                  .Bind("@email", email)
                  .Bind("@roleId", roleId)
                  .Bind("@isActive", isActive), ct);
    }

    public async Task UpdatePasswordAsync(int userId, byte[] passwordHash, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
        UPDATE Users
        SET PasswordHash = @hash, PasswordAlgo = 'argon2id', UpdatedAt = NOW()
        WHERE UserId = @id",
            c => c.Bind("@id", userId).Bind("@hash", passwordHash), ct);
    }
}