namespace EducationServer.Data.Abstractions;

public sealed record GradeSheet(
    int ScheduleId,
    int MonthScheduleId,
    DateOnly Date,
    string DisciplineTitle,
    string GroupTitle,
    byte PairNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<StudentGradeRow> Students,
    IReadOnlyList<GradeOption> Grades);

public sealed record StudentGradeRow(
    int StudentId,
    string FirstName,
    string LastName,
    string? Patronymic,
    int? GradeId,
    int? GradeValue,
    string? Comment);

public sealed record GradeOption(int GradeId, int GradeValue);

public interface IGradeSheetRepository
{
    Task<IReadOnlyList<GradeOption>> GetGradesAsync(CancellationToken ct);
    Task<GradeSheet?> GetSheetAsync(int monthScheduleId, CancellationToken ct);
    Task SaveAsync(
        int scheduleId,
        int markedByUserId,
        IReadOnlyList<(int StudentId, int GradeId, string? Comment)> marks,
        CancellationToken ct);
}