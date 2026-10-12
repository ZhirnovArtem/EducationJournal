using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class DisciplineRepository : IDisciplineRepository
{
    private readonly IDbConnectionFactory _factory;
    public DisciplineRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<DisciplineListItem>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT DisciplineId, Title, CreatedAt, DeletedAt
            FROM Disciplines
            WHERE DeletedAt IS NULL
            ORDER BY Title
            OFFSET @offset LIMIT @limit";
        return await conn.QueryAsync(sql, r => new DisciplineListItem(
            r.GetInt32(0),
            r.GetString(1),
            r.GetDateTime(2),
            r.IsDBNull(3) ? null : r.GetDateTime(3)),
            c => c.Bind("@offset", (page - 1) * pageSize)
                  .Bind("@limit", pageSize), ct);
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Disciplines WHERE DeletedAt IS NULL", null, ct);
    }

    public async Task<DisciplineListItem?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT DisciplineId, Title, CreatedAt, DeletedAt
            FROM Disciplines
            WHERE DisciplineId = @id AND DeletedAt IS NULL";
        return await conn.QueryFirstAsync(sql, r => new DisciplineListItem(
            r.GetInt32(0),
            r.GetString(1),
            r.GetDateTime(2),
            r.IsDBNull(3) ? null : r.GetDateTime(3)),
            c => c.Bind("@id", id), ct);
    }

    public async Task<int> InsertAsync(string title, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Disciplines (Title)
            VALUES (@title)
            RETURNING DisciplineId";
        return await conn.ExecuteScalarAsync<int>(sql,
            c => c.Bind("@title", title), ct);
    }

    public async Task UpdateAsync(int id, string title, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Disciplines SET Title = @title
            WHERE DisciplineId = @id",
            c => c.Bind("@id", id).Bind("@title", title), ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE Disciplines SET DeletedAt = NOW()
            WHERE DisciplineId = @id",
            c => c.Bind("@id", id), ct);
    }
}