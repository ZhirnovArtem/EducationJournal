using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class GroupRepository : IGroupRepository
{
    private readonly IDbConnectionFactory _factory;
    public GroupRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string BaseSelect = @"
        SELECT g.GroupId, g.Title, g.CreatedAt, g.DeletedAt,
               COUNT(s.StudentId) AS StudentCount
        FROM Groups g
        LEFT JOIN Students s ON s.GroupId = g.GroupId AND s.DeletedAt IS NULL
        WHERE g.DeletedAt IS NULL";

    public async Task<IReadOnlyList<GroupListItem>> ListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $@"
            {BaseSelect}
            GROUP BY g.GroupId
            ORDER BY g.Title
            OFFSET @offset LIMIT @limit";
        return await conn.QueryAsync(sql, r => new GroupListItem(
            r.GetInt32(0),
            r.GetString(1),
            r.GetDateTime(2),
            r.IsDBNull(3) ? null : r.GetDateTime(3),
            r.GetInt32(4)),
            c => c.Bind("@offset", (page - 1) * pageSize)
                  .Bind("@limit", pageSize), ct);
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Groups WHERE DeletedAt IS NULL", null, ct);
    }

    public async Task<GroupListItem?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $@"
            {BaseSelect} AND g.GroupId = @id
            GROUP BY g.GroupId";
        return await conn.QueryFirstAsync(sql, r => new GroupListItem(
            r.GetInt32(0),
            r.GetString(1),
            r.GetDateTime(2),
            r.IsDBNull(3) ? null : r.GetDateTime(3),
            r.GetInt32(4)),
            c => c.Bind("@id", id), ct);
    }

    public async Task<int> InsertAsync(string title, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Groups (Title) VALUES (@title)
            RETURNING GroupId";
        return await conn.ExecuteScalarAsync<int>(sql,
            c => c.Bind("@title", title), ct);
    }

    public async Task UpdateAsync(int id, string title, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Groups SET Title = @title WHERE GroupId = @id",
            c => c.Bind("@id", id).Bind("@title", title), ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Groups SET DeletedAt = NOW() WHERE GroupId = @id",
            c => c.Bind("@id", id), ct);
    }
}