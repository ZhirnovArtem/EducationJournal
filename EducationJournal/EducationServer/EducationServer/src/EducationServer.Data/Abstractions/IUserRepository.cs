using EducationServer.Domain;

namespace EducationServer.Data.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByLoginAsync(string login, CancellationToken ct);
    Task<User?> FindByIdAsync(int userId, CancellationToken ct);
    Task<int> InsertAsync(User user, CancellationToken ct);
    Task UpdateLastLoginAsync(int userId, CancellationToken ct);
    Task<int> IncrementFailedAttemptsAsync(int userId, CancellationToken ct);
    Task ResetFailedAttemptsAsync(int userId, CancellationToken ct);
    Task LockAsync(int userId, DateTime until, CancellationToken ct);
    Task<Role?> GetRoleAsync(int roleId, CancellationToken ct);
    Task InsertLoginAttemptAsync(LoginAttempt attempt, CancellationToken ct);
    Task SaveRefreshTokenAsync(int userId, string tokenHash, DateTime expiresAt,
        string? userAgent, CancellationToken ct);
    Task<RefreshTokenRow?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct);
    Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<UserListItem>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SoftDeleteAsync(int userId, CancellationToken ct);
    Task UpdateAsync(int userId, string? email, int roleId, bool isActive, CancellationToken ct);
    Task UpdatePasswordAsync(int userId, byte[] passwordHash, CancellationToken ct);

    // ── НОВЫЙ МЕТОД ──
    Task<IReadOnlyList<TeacherListItem>> GetTeachersByStudentIdAsync(int studentUserId, CancellationToken ct);
}