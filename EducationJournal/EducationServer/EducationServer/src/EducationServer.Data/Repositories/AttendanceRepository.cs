using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly IDbConnectionFactory _factory;
    public AttendanceRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<AttendanceStatusItem>> GetStatusesAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT AttendanceId, Title
            FROM Attendance
            ORDER BY AttendanceId";
        return await conn.QueryAsync(sql, r => new AttendanceStatusItem(
            r.GetInt32(0),
            r.GetString(1)), null, ct);
    }

    public async Task<AttendanceSheet?> GetSheetAsync(int monthScheduleId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var scheduleId = await EnsureScheduleAsync(conn, monthScheduleId, ct);
        if (scheduleId is null) return null;
        const string headSql = @"
            SELECT ms.Date, d.Title AS DisciplineTitle, g.Title AS GroupTitle,
                   lt.PairNumber, lt.StartTime, lt.EndTime,
                   ms.GroupId, s.ScheduleId
            FROM MonthShedule ms
            JOIN Disciplines d  ON d.DisciplineId = ms.DisciplineId
            JOIN Groups g       ON g.GroupId = ms.GroupId
            JOIN LessonTimes lt ON lt.LessonTimeId = ms.LessonTimeId
            JOIN Schedule s     ON s.MonthScheduleId = ms.MonthScheduleId
            WHERE ms.MonthScheduleId = @id AND ms.DeletedAt IS NULL";

        var head = await conn.QueryFirstAsync(headSql, r => new
        {
            Date = DateOnly.FromDateTime(r.GetDateTime(0)),
            DisciplineTitle = r.GetString(1),
            GroupTitle = r.GetString(2),
            PairNumber = (byte)r.GetInt16(3),
            StartTime = TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(4)),
            EndTime = TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(5)),
            GroupId = r.GetInt32(6),
            ScheduleId = r.GetInt32(7),
        }, c => c.Bind("@id", monthScheduleId), ct);

        if (head is null) return null;
        const string studentsSql = @"
            SELECT st.StudentId, st.FirstName, st.LastName, st.Patronymic,
                   sa.AttendanceId, a.Title AS AttendanceTitle, sa.Comment
            FROM Students st
            LEFT JOIN ScheduleAttendance sa
                ON sa.StudentId = st.StudentId AND sa.ScheduleId = @sid
            LEFT JOIN Attendance a ON a.AttendanceId = sa.AttendanceId
            WHERE st.GroupId = @gid AND st.DeletedAt IS NULL
            ORDER BY st.LastName, st.FirstName";

        var students = await conn.QueryAsync(studentsSql, r => new StudentAttendanceRow(
            r.GetInt32(0),
            r.GetString(1),
            r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.IsDBNull(4) ? null : r.GetInt32(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6)),
            c => c.Bind("@sid", head.ScheduleId).Bind("@gid", head.GroupId), ct);

        var statuses = await GetStatusesAsync(ct);

        return new AttendanceSheet(
            head.ScheduleId,
            monthScheduleId,
            head.Date,
            head.DisciplineTitle,
            head.GroupTitle,
            head.PairNumber,
            head.StartTime,
            head.EndTime,
            students,
            statuses);
    }

    public async Task SaveAsync(
        int scheduleId,
        int markedByUserId,
        IReadOnlyList<(int StudentId, int AttendanceId, string? Comment)> marks,
        CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string upsertSql = @"
            INSERT INTO ScheduleAttendance
                (ScheduleId, StudentId, AttendanceId, Comment, MarkedBy, MarkedAt)
            VALUES
                (@sid, @stid, @aid, @comment, @by, NOW())
            ON CONFLICT (ScheduleId, StudentId)
            DO UPDATE SET
                AttendanceId = EXCLUDED.AttendanceId,
                Comment      = EXCLUDED.Comment,
                MarkedBy     = EXCLUDED.MarkedBy,
                MarkedAt     = NOW()";

        foreach (var m in marks)
        {
            await conn.ExecuteAsync(upsertSql, c => c
                .Bind("@sid", scheduleId)
                .Bind("@stid", m.StudentId)
                .Bind("@aid", m.AttendanceId)
                .Bind("@comment", m.Comment)
                .Bind("@by", markedByUserId), ct);
        }
        await conn.ExecuteAsync(@"
            UPDATE Schedule SET Status = 'Conducted', ConductedAt = NOW()
            WHERE ScheduleId = @sid AND Status <> 'Conducted'",
            c => c.Bind("@sid", scheduleId), ct);

        await tx.CommitAsync(ct);
    }
    private static async Task<int?> EnsureScheduleAsync(
        System.Data.Common.DbConnection conn, int monthScheduleId, CancellationToken ct)
    {
        var exists = await conn.ExecuteScalarAsync<int?>(
            "SELECT MonthScheduleId FROM MonthShedule WHERE MonthScheduleId = @id AND DeletedAt IS NULL",
            c => c.Bind("@id", monthScheduleId), ct);
        if (exists is null) return null;
        var existing = await conn.ExecuteScalarAsync<int?>(
            "SELECT ScheduleId FROM Schedule WHERE MonthScheduleId = @id AND DeletedAt IS NULL",
            c => c.Bind("@id", monthScheduleId), ct);
        if (existing is not null) return existing;
        const string insertSql = @"
            INSERT INTO Schedule (MonthScheduleId, Status)
            VALUES (@id, 'Planned')
            RETURNING ScheduleId";
        return await conn.ExecuteScalarAsync<int>(
            insertSql, c => c.Bind("@id", monthScheduleId), ct);
    }
}