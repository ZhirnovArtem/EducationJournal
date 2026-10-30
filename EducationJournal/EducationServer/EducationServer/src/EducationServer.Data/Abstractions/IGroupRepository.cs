namespace EducationServer.Data.Abstractions;

public record GroupListItem(
	int GroupId,
	string Title,
	DateTime CreatedAt,
	DateTime? DeletedAt,
	int StudentCount);

public interface IGroupRepository
{
	Task<IReadOnlyList<GroupListItem>> ListAsync(int page, int pageSize, CancellationToken ct);
	Task<int> CountAsync(CancellationToken ct);
	Task<GroupListItem?> FindByIdAsync(int id, CancellationToken ct);
	Task<int> InsertAsync(string title, CancellationToken ct);
	Task UpdateAsync(int id, string title, CancellationToken ct);
	Task SoftDeleteAsync(int id, CancellationToken ct);
}