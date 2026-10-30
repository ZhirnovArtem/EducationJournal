namespace EducationServer.Data.Abstractions;

public record ScheduleItem(
    int MonthScheduleId,
    string Title,
    DateOnly Date,
    int LessonTimeId,
    byte PairNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int DisciplineId,
    string DisciplineTitle,
    int TeacherId,
    string TeacherName,
    int GroupId,
    string GroupTitle,
    string? Room,
    string LessonType,
    int? ScheduleId,        
    string? Status);         

public record LessonTimeItem(
    int LessonTimeId,
    byte PairNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int? BreakMinutes,
    bool IsActive);

public interface IScheduleRepository
{
    Task<IReadOnlyList<ScheduleItem>> GetRangeAsync(
        DateOnly from, DateOnly to,
        int? groupId, int? teacherId,
        CancellationToken ct);
    Task<IReadOnlyList<LessonTimeItem>> GetLessonTimesAsync(CancellationToken ct);
    Task<int> CreateMonthScheduleAsync(
        string title, int disciplineId, int teacherId, int groupId,
        int lessonTimeId, DateOnly date, string? room, string lessonType,
        CancellationToken ct);
    Task UpdateMonthScheduleAsync(
        int monthScheduleId, string title, int disciplineId, int teacherId,
        int groupId, int lessonTimeId, DateOnly date, string? room, string lessonType,
        CancellationToken ct);
    Task CancelScheduleAsync(int monthScheduleId, CancellationToken ct);
    Task<ScheduleItem?> FindByIdAsync(int monthScheduleId, CancellationToken ct);
    Task<bool> HasConflictAsync(
        int groupId, int teacherId, int lessonTimeId, DateOnly date,
        int? excludeId, CancellationToken ct);
}