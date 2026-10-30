namespace EducationServer.Data.Abstractions;

public record DisciplineListItem(
    int DisciplineId,
    string Title,
    DateTime CreatedAt,
    DateTime? DeletedAt);

public interface IDisciplineRepository
{
    Task<IReadOnlyList<DisciplineListItem>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task<DisciplineListItem?> FindByIdAsync(int id, CancellationToken ct);
    Task<int> InsertAsync(string title, CancellationToken ct);
    Task UpdateAsync(int id, string title, CancellationToken ct);
    Task SoftDeleteAsync(int id, CancellationToken ct);
}