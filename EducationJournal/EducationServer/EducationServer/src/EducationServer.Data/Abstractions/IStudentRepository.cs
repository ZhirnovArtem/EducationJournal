namespace EducationServer.Data.Abstractions;

public record StudentListItem(
    int StudentId,
    int UserId,
    string Login,
    string FirstName,
    string LastName,
    string? Patronymic,
    string Email,
    int? GroupId,
    string? GroupTitle,
    DateTime CreatedAt);

public interface IStudentRepository
{
    Task<IReadOnlyList<StudentListItem>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task<StudentListItem?> FindByIdAsync(int id, CancellationToken ct);
    Task<StudentListItem?> FindByUserIdAsync(int userId, CancellationToken ct);
    Task<int> InsertAsync(int userId, string firstName, string lastName, string? patronymic,
        DateTime birthDate, string email, int? groupId, CancellationToken ct);
    Task UpdateAsync(int id, string firstName, string lastName, string? patronymic,
        string email, int? groupId, CancellationToken ct);
    Task UpdateGroupAsync(int id, int? groupId, CancellationToken ct);
    Task SoftDeleteAsync(int id, CancellationToken ct);
}