using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class StudentRepository : IStudentRepository
{
    private readonly IDbConnectionFactory _factory;
    public StudentRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string Select = @"
        SELECT s.StudentId, s.UserId, u.Login, s.FirstName, s.LastName, s.Patronymic,
               s.Email, s.GroupId, g.Title AS GroupTitle, s.CreatedAt
        FROM Students s
        JOIN Users u ON u.UserId = s.UserId
        LEFT JOIN Groups g ON g.GroupId = s.GroupId
        WHERE s.DeletedAt IS NULL";

    public async Task<IReadOnlyList<StudentListItem>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $@"
            {Select}
            ORDER BY s.LastName, s.FirstName
            OFFSET @offset LIMIT @limit";
        return await conn.QueryAsync(sql, Map,
            c => c.Bind("@offset", (page - 1) * pageSize)
                  .Bind("@limit", pageSize), ct);
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Students WHERE DeletedAt IS NULL", null, ct);
    }

    public async Task<StudentListItem?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $"{Select} AND s.StudentId = @id";
        return await conn.QueryFirstAsync(sql, Map, c => c.Bind("@id", id), ct);
    }

    public async Task<StudentListItem?> FindByUserIdAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $"{Select} AND s.UserId = @uid";
        return await conn.QueryFirstAsync(sql, Map, c => c.Bind("@uid", userId), ct);
    }

    public async Task<int> InsertAsync(int userId, string firstName, string lastName,
        string? patronymic, DateTime birthDate, string email, int? groupId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Students (UserId, FirstName, LastName, Patronymic, BirthDate, Email, GroupId)
            VALUES (@uid, @fn, @ln, @pat, @bd, @email, @gid)
            RETURNING StudentId";
        return await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@uid", userId)
            .Bind("@fn", firstName)
            .Bind("@ln", lastName)
            .Bind("@pat", patronymic)
            .Bind("@bd", birthDate)
            .Bind("@email", email)
            .Bind("@gid", groupId), ct);
    }

    public async Task UpdateAsync(int id, string firstName, string lastName,
        string? patronymic, string email, int? groupId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Students
            SET FirstName = @fn, LastName = @ln, Patronymic = @pat,
                Email = @email, GroupId = @gid, UpdatedAt = NOW()
            WHERE StudentId = @id",
            c => c.Bind("@id", id)
                  .Bind("@fn", firstName)
                  .Bind("@ln", lastName)
                  .Bind("@pat", patronymic)
                  .Bind("@email", email)
                  .Bind("@gid", groupId), ct);
    }

    public async Task UpdateGroupAsync(int id, int? groupId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Students SET GroupId = @gid, UpdatedAt = NOW()
            WHERE StudentId = @id",
            c => c.Bind("@id", id).Bind("@gid", groupId), ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Students SET DeletedAt = NOW() WHERE StudentId = @id",
            c => c.Bind("@id", id), ct);
    }

    private static StudentListItem Map(System.Data.Common.DbDataReader r) => new(
        r.GetInt32(0),
        r.GetInt32(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5),
        r.GetString(6),
        r.IsDBNull(7) ? null : r.GetInt32(7),
        r.IsDBNull(8) ? null : r.GetString(8),
        r.GetDateTime(9));
}