using EducationServer.Domain;

namespace EducationServer.Data.Abstractions;

public interface IFileRepository
{
    Task<long> InsertAsync(FileEntity file, CancellationToken ct);
    Task<FileEntity?> FindByIdAsync(long id, CancellationToken ct);
    Task SoftDeleteAsync(long id, CancellationToken ct);
}