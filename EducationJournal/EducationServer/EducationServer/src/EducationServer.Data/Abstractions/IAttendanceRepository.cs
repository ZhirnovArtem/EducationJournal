namespace EducationServer.Data.Abstractions;

public record AttendanceStatusItem(
    int AttendanceId,
    string Title);

public record StudentAttendanceRow(
    int StudentId,
    string FirstName,
    string LastName,
    string? Patronymic,
    int? AttendanceId,
    string? AttendanceTitle,
    string? Comment);

public record AttendanceSheet(
    int ScheduleId,
    int MonthScheduleId,
    DateOnly Date,
    string DisciplineTitle,
    string GroupTitle,
    byte PairNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<StudentAttendanceRow> Students,
    IReadOnlyList<AttendanceStatusItem> Statuses);

public interface IAttendanceRepository
{
    Task<AttendanceSheet?> GetSheetAsync(int monthScheduleId, CancellationToken ct);
    Task<IReadOnlyList<AttendanceStatusItem>> GetStatusesAsync(CancellationToken ct);
    Task SaveAsync(
        int scheduleId,
        int markedByUserId,
        IReadOnlyList<(int StudentId, int AttendanceId, string? Comment)> marks,
        CancellationToken ct);
}