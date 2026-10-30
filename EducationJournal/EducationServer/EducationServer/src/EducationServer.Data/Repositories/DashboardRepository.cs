using Dapper;
using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _factory;
    public DashboardRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<int?> GetStudentIdByUserAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int?>(
            "SELECT StudentId FROM Students WHERE UserId = @uid AND DeletedAt IS NULL",
            c => c.Bind("@uid", userId), ct);
    }

    public async Task<decimal?> GetAverageGradeAsync(int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT AVG(g.GradeValue)::numeric(3,2)
            FROM Homeworks h
            JOIN Grades g ON g.GradeId = h.GradeId
            WHERE h.StudentId = @sid
              AND h.DeletedAt IS NULL
              AND h.GradeId IS NOT NULL";
        return await conn.ExecuteScalarAsync<decimal?>(sql, c => c.Bind("@sid", studentId), ct);
    }

    public async Task<int> CountActiveHomeworksAsync(int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT COUNT(*)
            FROM Homeworks
            WHERE StudentId = @sid
              AND DeletedAt IS NULL
              AND DueDate IS NULL
              AND GradeId IS NULL
              AND DeadLine >= CURRENT_DATE";
        return await conn.ExecuteScalarAsync<int>(sql, c => c.Bind("@sid", studentId), ct);
    }

    public async Task<int?> GetAttendancePercentAsync(int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT
                CASE WHEN COUNT(*) = 0 THEN NULL
                     ELSE ROUND(100.0 * COUNT(*) FILTER (WHERE sa.AttendanceId = 1) / COUNT(*))
                END
            FROM ScheduleAttendance sa
            JOIN Schedule s ON s.ScheduleId = sa.ScheduleId
            WHERE sa.StudentId = @sid
              AND s.DeletedAt IS NULL";
        return await conn.ExecuteScalarAsync<int?>(sql, c => c.Bind("@sid", studentId), ct);
    }

    public async Task<(int? Rank, int? Total)> GetClassRankAsync(int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            WITH student_avg AS (
                SELECT h.StudentId, AVG(g.GradeValue)::numeric AS avg_grade
                FROM Homeworks h
                JOIN Grades g ON g.GradeId = h.GradeId
                JOIN Students s ON s.StudentId = h.StudentId
                WHERE h.DeletedAt IS NULL AND h.GradeId IS NOT NULL
                  AND s.GroupId = (SELECT GroupId FROM Students WHERE StudentId = @sid)
                GROUP BY h.StudentId
            ),
            ranked AS (
                SELECT StudentId,
                       RANK() OVER (ORDER BY avg_grade DESC) AS rnk,
                       COUNT(*) OVER () AS total
                FROM student_avg
            )
            SELECT rnk, total FROM ranked WHERE StudentId = @sid";

        var row = await conn.QueryFirstOrDefaultAsync<RankRow>(
            sql,
            new { sid = studentId });

        if (row is null) return (null, null);
        return (row.Rnk, row.Total);
    }

    public async Task<IReadOnlyList<UpcomingLessonDto>> GetUpcomingLessonsAsync(
        int studentId, int take, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT ms.Date,
                   lt.StartTime, lt.EndTime,
                   d.Title AS DisciplineTitle,
                   ms.Room,
                   ms.LessonType,
                   t.LastName || ' ' || t.FirstName AS TeacherName
            FROM MonthShedule ms
            JOIN LessonTimes lt ON lt.LessonTimeId = ms.LessonTimeId
            JOIN Disciplines d  ON d.DisciplineId = ms.DisciplineId
            JOIN Teachers t     ON t.TeacherId = ms.TeacherId
            WHERE ms.GroupId = (SELECT GroupId FROM Students WHERE StudentId = @sid)
              AND ms.Date >= CURRENT_DATE
              AND ms.DeletedAt IS NULL
            ORDER BY ms.Date, lt.StartTime
            LIMIT @take";

        return await conn.QueryAsync(sql, r => new UpcomingLessonDto(
            DateOnly.FromDateTime(r.GetDateTime(0)).ToString("yyyy-MM-dd"),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(1)).ToString("HH:mm"),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(2)).ToString("HH:mm"),
            r.GetString(3),
            r.IsDBNull(4) ? "" : r.GetString(4),
            r.IsDBNull(5) ? "" : r.GetString(5),
            r.GetString(6)),
            c => c.Bind("@sid", studentId).Bind("@take", take), ct);
    }

    public async Task<IReadOnlyList<ActivityItemDto>> GetRecentActivityAsync(
        int studentId, int take, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            (SELECT 'Получена оценка ' || g.GradeValue || ' за ' || d.Title AS Text, h.UpdatedAt AS At
             FROM Homeworks h
             JOIN Grades g ON g.GradeId = h.GradeId
             JOIN Disciplines d ON d.DisciplineId = h.DisciplineId
             WHERE h.StudentId = @sid AND h.GradeId IS NOT NULL AND h.DeletedAt IS NULL
             ORDER BY h.UpdatedAt DESC LIMIT @take)
            UNION ALL
            (SELECT 'Сдано ДЗ: ' || h.Task AS Text, h.DueDate::timestamp AS At
             FROM Homeworks h
             WHERE h.StudentId = @sid AND h.DueDate IS NOT NULL AND h.DeletedAt IS NULL
             ORDER BY h.DueDate DESC LIMIT @take)
            ORDER BY At DESC LIMIT @take";
        return await conn.QueryAsync(sql, r => new ActivityItemDto(
            r.GetString(0),
            r.GetDateTime(1)), c => c.Bind("@sid", studentId).Bind("@take", take), ct);
    }

    public async Task<int> GetTeacherLessonsTodayCountAsync(int teacherUserId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT COUNT(*)
            FROM MonthShedule ms
            JOIN Teachers t ON t.TeacherId = ms.TeacherId
            WHERE t.UserId = @uid
              AND ms.Date = CURRENT_DATE
              AND ms.DeletedAt IS NULL";
        return await conn.ExecuteScalarAsync<int>(sql,
            c => c.Bind("@uid", teacherUserId), ct);
    }

    public async Task<IReadOnlyList<UpcomingLessonDto>> GetUpcomingLessonsForTeacherAsync(
    int teacherUserId, int take, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT ms.Date,
                   lt.StartTime, lt.EndTime,
                   d.Title AS DisciplineTitle,
                   ms.Room,
                   ms.LessonType,
                   g.Title AS GroupTitle
            FROM MonthShedule ms
            JOIN LessonTimes lt ON lt.LessonTimeId = ms.LessonTimeId
            JOIN Disciplines d  ON d.DisciplineId = ms.DisciplineId
            JOIN Groups g       ON g.GroupId = ms.GroupId
            JOIN Teachers t     ON t.TeacherId = ms.TeacherId
            WHERE t.UserId = @uid
              AND ms.Date >= CURRENT_DATE
              AND ms.DeletedAt IS NULL
            ORDER BY ms.Date, lt.StartTime
            LIMIT @take";

        return await conn.QueryAsync(sql, r => new UpcomingLessonDto(
            DateOnly.FromDateTime(r.GetDateTime(0)).ToString("yyyy-MM-dd"),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(1)).ToString("HH:mm"),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(2)).ToString("HH:mm"),
            r.GetString(3),                       // DisciplineTitle
            r.IsDBNull(4) ? "" : r.GetString(4),  // Room
            r.IsDBNull(5) ? "" : r.GetString(5),  // LessonType
            r.GetString(6)),                      // GroupTitle (вместо TeacherName)
            c => c.Bind("@uid", teacherUserId).Bind("@take", take), ct);
    }

    internal sealed class RankRow
    {
        public int Rnk { get; set; }
        public int Total { get; set; }
    }
}