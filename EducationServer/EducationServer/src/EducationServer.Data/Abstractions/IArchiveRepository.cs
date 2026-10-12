using EducationServer.Domain;

namespace EducationServer.Data.Abstractions;

public interface IArchiveRepository
{
    Task<long> InsertArchiveAsync(HomeworkArchive archive, CancellationToken ct);
    Task InsertEntriesAsync(long archiveId,
        IReadOnlyList<HomeworkArchiveFile> entries, CancellationToken ct);
    Task<HomeworkArchive?> FindByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<HomeworkArchiveFile>> ListEntriesAsync(long archiveId, CancellationToken ct);
    Task SoftDeleteAsync(long archiveId, CancellationToken ct);
}