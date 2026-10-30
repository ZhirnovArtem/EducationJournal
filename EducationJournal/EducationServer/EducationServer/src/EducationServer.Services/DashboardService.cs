using EducationServer.Data.Abstractions;

namespace EducationServer.Services;

public record DashboardDto(
    decimal? AverageGrade,
    int ActiveHomeworks,
    int? AttendancePercent,
    int? ClassRank,
    int? ClassSize,
    IReadOnlyList<UpcomingLessonDto> UpcomingLessons,
    IReadOnlyList<ActivityItemDto> RecentActivity,
    int? TodayLessonCount);

public sealed class DashboardService
{
    private readonly IDashboardRepository _dash;

    public DashboardService(IDashboardRepository dash) => _dash = dash;

    public async Task<DashboardDto> GetAsync(int userId, string role, CancellationToken ct)
    {
        if (role == "Student")
            return await BuildForStudentAsync(userId, ct);

        if (role == "Teacher")
            return await BuildForTeacherAsync(userId, ct);

        // Admin — пока пустой
        return new DashboardDto(
            null, 0, null, null, null,
            Array.Empty<UpcomingLessonDto>(),
            Array.Empty<ActivityItemDto>(),
            null);
    }

    private async Task<DashboardDto> BuildForStudentAsync(int userId, CancellationToken ct)
    {
        var studentId = await _dash.GetStudentIdByUserAsync(userId, ct);
        if (studentId is null)
            return new DashboardDto(
                null, 0, null, null, null,
                Array.Empty<UpcomingLessonDto>(),
                Array.Empty<ActivityItemDto>(),
                null);

        var avg = await _dash.GetAverageGradeAsync(studentId.Value, ct);
        var activeHw = await _dash.CountActiveHomeworksAsync(studentId.Value, ct);
        var attendance = await _dash.GetAttendancePercentAsync(studentId.Value, ct);
        var (rank, total) = await _dash.GetClassRankAsync(studentId.Value, ct);
        var lessons = await _dash.GetUpcomingLessonsAsync(studentId.Value, 3, ct);
        var activity = await _dash.GetRecentActivityAsync(studentId.Value, 5, ct);

        return new DashboardDto(
            avg, activeHw, attendance, rank, total, lessons, activity,
            null);                         // ⬅️ TodayLessonCount для студента null
    }

    private async Task<DashboardDto> BuildForTeacherAsync(int userId, CancellationToken ct)
    {
        var lessonsToday = await _dash.GetTeacherLessonsTodayCountAsync(userId, ct);
        var upcoming = await _dash.GetUpcomingLessonsForTeacherAsync(userId, 3, ct);

        return new DashboardDto(
            AverageGrade: null,
            ActiveHomeworks: 0,
            AttendancePercent: null,
            ClassRank: null,
            ClassSize: null,
            UpcomingLessons: upcoming,
            RecentActivity: Array.Empty<ActivityItemDto>(),
            TodayLessonCount: lessonsToday);
    }
}