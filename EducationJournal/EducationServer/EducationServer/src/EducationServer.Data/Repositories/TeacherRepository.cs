using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class TeacherRepository : ITeacherRepository
{
    private readonly IDbConnectionFactory _factory;
    public TeacherRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<TeacherListItem>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        const string teacherSql = @"
            SELECT t.TeacherId, t.UserId, u.Login, t.FirstName, t.LastName, t.Patronymic,
                   t.Email, t.CreatedAt
            FROM Teachers t
            JOIN Users u ON u.UserId = t.UserId
            WHERE t.DeletedAt IS NULL
            ORDER BY t.LastName, t.FirstName
            OFFSET @offset LIMIT @limit";

        var teachers = await conn.QueryAsync(teacherSql, r => new
        {
            TeacherId = r.GetInt32(0),
            UserId = r.GetInt32(1),
            Login = r.GetString(2),
            FirstName = r.GetString(3),
            LastName = r.GetString(4),
            Patronymic = r.IsDBNull(5) ? (string?)null : r.GetString(5),
            Email = r.GetString(6),
            CreatedAt = r.GetDateTime(7),
        }, c => c.Bind("@offset", (page - 1) * pageSize).Bind("@limit", pageSize), ct);

        if (teachers.Count == 0) return Array.Empty<TeacherListItem>();

        var ids = teachers.Select(t => t.TeacherId).ToArray();
        const string discSql = @"
            SELECT td.TeacherId, d.DisciplineId, d.Title
            FROM TeacherDisciplines td
            JOIN Disciplines d ON d.DisciplineId = td.DisciplineId
            WHERE td.TeacherId = ANY(@ids)";

        var links = await conn.QueryAsync(discSql, r => new
        {
            TeacherId = r.GetInt32(0),
            DisciplineId = r.GetInt32(1),
            Title = r.GetString(2),
        }, c => c.Bind("@ids", ids), ct);

        var map = links
            .GroupBy(x => x.TeacherId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.DisciplineId, x.Title)).ToList());

        return teachers.Select(t =>
        {
            var discs = map.TryGetValue(t.TeacherId, out var list)
                ? list
                : new List<(int DisciplineId, string Title)>();

            return new TeacherListItem(
                t.TeacherId, t.UserId, t.Login, t.FirstName, t.LastName, t.Patronymic,
                t.Email,
                discs.Select(x => x.DisciplineId).ToList(),
                discs.Select(x => x.Title).ToList(),
                t.CreatedAt);
        }).ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Teachers WHERE DeletedAt IS NULL", null, ct);
    }

    public async Task<TeacherListItem?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT t.TeacherId, t.UserId, u.Login, t.FirstName, t.LastName, t.Patronymic,
                   t.Email, t.CreatedAt
            FROM Teachers t
            JOIN Users u ON u.UserId = t.UserId
            WHERE t.DeletedAt IS NULL AND t.TeacherId = @id";
        var t = await conn.QueryFirstAsync(sql, r => new TeacherListItem(
            r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.GetString(6), new List<int>(), new List<string>(), r.GetDateTime(7)),
            c => c.Bind("@id", id), ct);

        if (t is null) return null;

        const string discSql = @"
            SELECT d.DisciplineId, d.Title
            FROM TeacherDisciplines td
            JOIN Disciplines d ON d.DisciplineId = td.DisciplineId
            WHERE td.TeacherId = @tid";
        var links = await conn.QueryAsync(discSql, r => new
        {
            Id = r.GetInt32(0),
            Title = r.GetString(1),
        }, c => c.Bind("@tid", id), ct);

        return t with
        {
            DisciplineIds = links.Select(x => x.Id).ToList(),
            DisciplineTitles = links.Select(x => x.Title).ToList(),
        };
    }

    public async Task<TeacherListItem?> FindByUserIdAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT TeacherId FROM Teachers WHERE UserId = @uid AND DeletedAt IS NULL",
            c => c.Bind("@uid", userId), ct);
        return id.HasValue ? await FindByIdAsync(id.Value, ct) : null;
    }

    public async Task UpdateAsync(int id, string firstName, string lastName,
        string? patronymic, string email, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Teachers
            SET FirstName = @fn, LastName = @ln, Patronymic = @pat,
                Email = @email, UpdatedAt = NOW()
            WHERE TeacherId = @id",
            c => c.Bind("@id", id)
                  .Bind("@fn", firstName)
                  .Bind("@ln", lastName)
                  .Bind("@pat", patronymic)
                  .Bind("@email", email), ct);
    }

    public async Task SetDisciplinesAsync(int teacherId,
        IReadOnlyList<int> disciplineIds, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(
            "DELETE FROM TeacherDisciplines WHERE TeacherId = @id",
            c => c.Bind("@id", teacherId), ct);

        foreach (var did in disciplineIds.Distinct())
        {
            await conn.ExecuteAsync(
                "INSERT INTO TeacherDisciplines (TeacherId, DisciplineId) VALUES (@tid, @did)",
                c => c.Bind("@tid", teacherId).Bind("@did", did), ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Teachers SET DeletedAt = NOW() WHERE TeacherId = @id",
            c => c.Bind("@id", id), ct);
    }

    public async Task<int> InsertAsync(int userId, string firstName, string lastName,
    string? patronymic, string email, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
        INSERT INTO Teachers (UserId, FirstName, LastName, Patronymic, Email)
        VALUES (@uid, @fn, @ln, @pat, @email)
        RETURNING TeacherId";
        return await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@uid", userId)
            .Bind("@fn", firstName)
            .Bind("@ln", lastName)
            .Bind("@pat", patronymic)
            .Bind("@email", email), ct);
    }
}