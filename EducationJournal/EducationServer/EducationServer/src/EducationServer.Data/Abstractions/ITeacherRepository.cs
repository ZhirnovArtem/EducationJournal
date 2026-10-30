namespace EducationServer.Data.Abstractions;

public record TeacherListItem(
    int TeacherId,
    int UserId,
    string Login,
    string FirstName,
    string LastName,
    string? Patronymic,
    string Email,
    List<int> DisciplineIds,
    List<string> DisciplineTitles,
    DateTime CreatedAt);

public interface ITeacherRepository
{
    Task<IReadOnlyList<TeacherListItem>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task<TeacherListItem?> FindByIdAsync(int id, CancellationToken ct);
    Task<TeacherListItem?> FindByUserIdAsync(int userId, CancellationToken ct);
    Task UpdateAsync(int id, string firstName, string lastName, string? patronymic,
        string email, CancellationToken ct);
    Task SetDisciplinesAsync(int teacherId, IReadOnlyList<int> disciplineIds, CancellationToken ct);
    Task SoftDeleteAsync(int id, CancellationToken ct);
    Task<int> InsertAsync(int userId, string firstName, string lastName,
    string? patronymic, string email, CancellationToken ct);
}