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

    // ⬇️ ЭТОЙ СТРОКИ НЕ ХВАТАЛО
    Task UpdateSubmissionAsync(int homeworkId, string? text,
        long? answerFileId, long? answerArchiveId, CancellationToken ct);

    Task UpdateGradeAsync(int homeworkId, int? gradeId, string? comment, CancellationToken ct);

    Task SoftDeleteAsync(int homeworkId, CancellationToken ct);

    Task<long?> GetArchiveIdAsync(int homeworkId, CancellationToken ct);

    Task<(IReadOnlyList<SubmissionListItem> Items, int Total)> ListSubmissionsAsync(
        int teacherUserId,
        int? disciplineId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<int> GetStudentIdByUserAsync(int userId, CancellationToken ct);
    Task<int?> GetTeacherIdByUserIdAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<int>> GetStudentIdsByGroupAsync(int groupId, CancellationToken ct);
    Task<int> AutoGradeOverdueAsync(int studentId, CancellationToken ct);

    Task<int> CreateBatchAsync(
        int teacherId,
        int disciplineId,
        IReadOnlyList<int> studentIds,
        string task,
        long? taskFileId,
        DateOnly dateOfIssue,
        DateOnly deadLine,
        CancellationToken ct);
}

public sealed record SubmissionListItem(
    int HomeworkId,
    int StudentId,
    string StudentName,
    string Task,
    string Discipline,
    DateOnly DeadLine,
    DateOnly? DueDate,
    int? Grade,
    string? TeacherComment,
    long? AnswerFileId,
    long? AnswerArchiveId);