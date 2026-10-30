using EducationServer.Data.Abstractions;
using EducationServer.Domain;

namespace EducationServer.Data.Repositories;

public sealed class ArchiveRepository : IArchiveRepository
{
    private readonly IDbConnectionFactory _factory;
    public ArchiveRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<long> InsertArchiveAsync(HomeworkArchive a, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO HomeworkArchives
                (FileId, OriginalName, FileCount, UncompressedBytes, CompressedBytes,
                 CompressionRatio, Algorithm, IsEncrypted, PasswordHash, Checksum)
            VALUES
                (@fid, @name, @cnt, @un, @comp,
                 @ratio, @algo, @enc, @pwd, @sha)
            RETURNING ArchiveId";
        return await conn.ExecuteScalarAsync<long>(sql, c => c
            .Bind("@fid", a.FileId)
            .Bind("@name", a.OriginalName)
            .Bind("@cnt", a.FileCount)
            .Bind("@un", a.UncompressedBytes)
            .Bind("@comp", a.CompressedBytes)
            .Bind("@ratio", a.CompressionRatio)
            .Bind("@algo", a.Algorithm)
            .Bind("@enc", a.IsEncrypted)
            .Bind("@pwd", a.PasswordHash)
            .Bind("@sha", a.Checksum), ct);
    }

    public async Task InsertEntriesAsync(long archiveId,
        IReadOnlyList<HomeworkArchiveFile> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return;
        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string sql = @"
            INSERT INTO HomeworkArchiveFiles
                (ArchiveId, PathInArchive, MimeType, SizeBytes, CompressedSize, Checksum)
            VALUES
                (@aid, @p, @m, @s, @cs, @sha)";

        foreach (var e in entries)
        {
            await conn.ExecuteAsync(sql, c => c
                .Bind("@aid", archiveId)
                .Bind("@p", e.PathInArchive)
                .Bind("@m", e.MimeType)
                .Bind("@s", e.SizeBytes)
                .Bind("@cs", e.CompressedSize)
                .Bind("@sha", e.Checksum), ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<HomeworkArchive?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT ArchiveId, FileId, OriginalName, FileCount, UncompressedBytes,
                   CompressedBytes, CompressionRatio, Algorithm, IsEncrypted,
                   PasswordHash, Checksum, CreatedAt, DeletedAt
            FROM HomeworkArchives
            WHERE ArchiveId = @id AND DeletedAt IS NULL";
        return await conn.QueryFirstAsync(sql, r => new HomeworkArchive
        {
            ArchiveId = r.GetInt64(0),
            FileId = r.GetInt64(1),
            OriginalName = r.GetString(2),
            FileCount = r.GetInt32(3),
            UncompressedBytes = r.GetInt64(4),
            CompressedBytes = r.GetInt64(5),
            CompressionRatio = r.IsDBNull(6) ? null : r.GetDecimal(6),
            Algorithm = r.GetString(7),
            IsEncrypted = r.GetBoolean(8),
            PasswordHash = r.IsDBNull(9) ? null : (byte[])r[9],
            Checksum = r.GetString(10),
            CreatedAt = r.GetDateTime(11),
            DeletedAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
        }, c => c.Bind("@id", id), ct);
    }

    public async Task<IReadOnlyList<HomeworkArchiveFile>> ListEntriesAsync(
        long archiveId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT ArchiveFileId, ArchiveId, PathInArchive, MimeType,
                   SizeBytes, CompressedSize, Checksum
            FROM HomeworkArchiveFiles
            WHERE ArchiveId = @id
            ORDER BY PathInArchive";
        return await conn.QueryAsync(sql, r => new HomeworkArchiveFile
        {
            ArchiveFileId = r.GetInt64(0),
            ArchiveId = r.GetInt64(1),
            PathInArchive = r.GetString(2),
            MimeType = r.IsDBNull(3) ? null : r.GetString(3),
            SizeBytes = r.GetInt64(4),
            CompressedSize = r.IsDBNull(5) ? null : r.GetInt64(5),
            Checksum = r.IsDBNull(6) ? null : r.GetString(6),
        }, c => c.Bind("@id", archiveId), ct);
    }

    public async Task SoftDeleteAsync(long archiveId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE HomeworkArchives SET DeletedAt = NOW() WHERE ArchiveId = @id",
            c => c.Bind("@id", archiveId), ct);
    }
}