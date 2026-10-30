namespace EducationServer.Data.Abstractions;

public interface IDashboardRepository
{
    Task<int?> GetStudentIdByUserAsync(int userId, CancellationToken ct);
    Task<decimal?> GetAverageGradeAsync(int studentId, CancellationToken ct);
    Task<int> CountActiveHomeworksAsync(int studentId, CancellationToken ct);
    Task<int?> GetAttendancePercentAsync(int studentId, CancellationToken ct);
    Task<(int? Rank, int? Total)> GetClassRankAsync(int studentId, CancellationToken ct);
    Task<IReadOnlyList<UpcomingLessonDto>> GetUpcomingLessonsAsync(int studentId, int take, CancellationToken ct);
    Task<IReadOnlyList<ActivityItemDto>> GetRecentActivityAsync(int studentId, int take, CancellationToken ct);
    Task<int> GetTeacherLessonsTodayCountAsync(int teacherUserId, CancellationToken ct);
    Task<IReadOnlyList<UpcomingLessonDto>> GetUpcomingLessonsForTeacherAsync(
    int teacherUserId, int take, CancellationToken ct);
}

public record UpcomingLessonDto(
    string Date, string StartTime, string EndTime,
    string DisciplineTitle, string Room, string LessonType, string TeacherName);

public record ActivityItemDto(string Text, DateTime At);