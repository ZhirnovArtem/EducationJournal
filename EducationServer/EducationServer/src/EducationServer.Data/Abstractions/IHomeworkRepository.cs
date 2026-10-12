using EducationServer.Domain;

namespace EducationServer.Data.Abstractions;

public record HomeworkQuery(
    int? StudentId, int? DisciplineId,
    string? Status, DateOnly? From, DateOnly? To,
    int Page, int PageSize);

public interface IHomeworkRepository
{
    Task<(IReadOnlyList<HomeworkListItem> Items, int Total)> ListAsync(
        HomeworkQuery q, CancellationToken ct);

    Task<Homework?> FindByIdAsync(int id, CancellationToken ct);
    Task<int> InsertAsync(Homework hw, CancellationToken ct);

    Task UpdateSubmissionAsync(int homeworkId, string? text,
        long? answerFileId, long? answerArchiveId, CancellationToken ct);

    Task UpdateGradeAsync(int homeworkId, int? gradeId, CancellationToken ct);

    Task SoftDeleteAsync(int homeworkId, CancellationToken ct);

    Task<long?> GetArchiveIdAsync(int homeworkId, CancellationToken ct);

    Task<int> GetStudentIdByUserAsync(int userId, CancellationToken ct);
}