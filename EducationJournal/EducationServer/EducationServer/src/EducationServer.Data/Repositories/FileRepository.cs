using EducationServer.Data.Abstractions;
using EducationServer.Domain;

namespace EducationServer.Data.Repositories;

public sealed class FileRepository : IFileRepository
{
    private readonly IDbConnectionFactory _factory;
    public FileRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<long> InsertAsync(FileEntity file, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Files
                (Bucket, ObjectKey, OriginalName, MimeType, SizeBytes,
                 Checksum, UploadedBy)
            VALUES
                (@b, @k, @n, @m, @s, @c, @u)
            RETURNING FileId";
        return await conn.ExecuteScalarAsync<long>(sql, c => c
            .Bind("@b", file.Bucket)
            .Bind("@k", file.ObjectKey)
            .Bind("@n", file.OriginalName)
            .Bind("@m", file.MimeType)
            .Bind("@s", file.SizeBytes)
            .Bind("@c", file.Checksum)
            .Bind("@u", file.UploadedBy), ct);
    }

    public async Task<FileEntity?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT FileId, Bucket, ObjectKey, OriginalName, MimeType,
                   SizeBytes, Checksum, UploadedBy, UploadedAt, DeletedAt
            FROM Files
            WHERE FileId = @id AND DeletedAt IS NULL";
        return await conn.QueryFirstAsync(sql, r => new FileEntity
        {
            FileId = r.GetInt64(0),
            Bucket = r.GetString(1),
            ObjectKey = r.GetString(2),
            OriginalName = r.GetString(3),
            MimeType = r.GetString(4),
            SizeBytes = r.GetInt64(5),
            Checksum = r.GetString(6),
            UploadedBy = r.GetInt32(7),
            UploadedAt = r.GetDateTime(8),
            DeletedAt = r.IsDBNull(9) ? null : r.GetDateTime(9),
        }, c => c.Bind("@id", id), ct);
    }

    public async Task SoftDeleteAsync(long id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Files SET DeletedAt = NOW() WHERE FileId = @id",
            c => c.Bind("@id", id), ct);
    }
}