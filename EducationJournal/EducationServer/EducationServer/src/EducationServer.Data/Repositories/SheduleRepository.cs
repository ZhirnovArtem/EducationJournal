using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class ScheduleRepository : IScheduleRepository
{
    private readonly IDbConnectionFactory _factory;
    public ScheduleRepository(IDbConnectionFactory factory) => _factory = factory;

    private const string BaseSelect = @"
        SELECT ms.MonthScheduleId, ms.Title, ms.Date,
               lt.LessonTimeId, lt.PairNumber, lt.StartTime, lt.EndTime,
               d.DisciplineId, d.Title AS DisciplineTitle,
               t.TeacherId, t.LastName || ' ' || t.FirstName AS TeacherName,
               g.GroupId, g.Title AS GroupTitle,
               ms.Room, ms.LessonType,
               s.ScheduleId, s.Status
        FROM MonthShedule ms
        JOIN LessonTimes lt ON lt.LessonTimeId = ms.LessonTimeId
        JOIN Disciplines d  ON d.DisciplineId = ms.DisciplineId
        JOIN Teachers t     ON t.TeacherId = ms.TeacherId
        JOIN Groups g       ON g.GroupId = ms.GroupId
        LEFT JOIN Schedule s ON s.MonthScheduleId = ms.MonthScheduleId AND s.DeletedAt IS NULL
        WHERE ms.DeletedAt IS NULL";

    public async Task<IReadOnlyList<ScheduleItem>> GetRangeAsync(
        DateOnly from, DateOnly to,
        int? groupId, int? teacherId,
        CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        var where = new List<string>();
        if (groupId.HasValue) where.Add("AND ms.GroupId = @gid");
        if (teacherId.HasValue) where.Add("AND ms.TeacherId = @tid");

        var sql = $@"
            {BaseSelect}
            AND ms.Date >= @from AND ms.Date <= @to
            {string.Join(" ", where)}
            ORDER BY ms.Date, lt.PairNumber";

        return await conn.QueryAsync(sql, Map, c =>
        {
            c.Bind("@from", from.ToDateTime(TimeOnly.MinValue))
             .Bind("@to", to.ToDateTime(TimeOnly.MinValue));
            if (groupId.HasValue) c.Bind("@gid", groupId.Value);
            if (teacherId.HasValue) c.Bind("@tid", teacherId.Value);
        }, ct);
    }

    public async Task<IReadOnlyList<LessonTimeItem>> GetLessonTimesAsync(CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT LessonTimeId, PairNumber, StartTime, EndTime, BreakMinutes, IsActive
            FROM LessonTimes
            WHERE IsActive = TRUE
            ORDER BY PairNumber";
        return await conn.QueryAsync(sql, r => new LessonTimeItem(
            r.GetInt32(0),
            (byte)r.GetInt16(1),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(2)),
            TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(3)),
            r.IsDBNull(4) ? null : r.GetInt32(4),
            r.GetBoolean(5)), null, ct);
    }

    public async Task<ScheduleItem?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var sql = $"{BaseSelect} AND ms.MonthScheduleId = @id";
        return await conn.QueryFirstAsync(sql, Map, c => c.Bind("@id", id), ct);
    }

    public async Task<int> CreateMonthScheduleAsync(
        string title, int disciplineId, int teacherId, int groupId,
        int lessonTimeId, DateOnly date, string? room, string lessonType,
        CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO MonthShedule
                (Title, DisciplineId, TeacherId, GroupId, LessonTimeId, Date, Room, LessonType)
            VALUES
                (@title, @did, @tid, @gid, @ltid, @date, @room, @type)
            RETURNING MonthScheduleId";
        return await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@title", title)
            .Bind("@did", disciplineId)
            .Bind("@tid", teacherId)
            .Bind("@gid", groupId)
            .Bind("@ltid", lessonTimeId)
            .Bind("@date", date.ToDateTime(TimeOnly.MinValue))
            .Bind("@room", room)
            .Bind("@type", lessonType), ct);
    }

    public async Task UpdateMonthScheduleAsync(
        int monthScheduleId, string title, int disciplineId, int teacherId,
        int groupId, int lessonTimeId, DateOnly date, string? room, string lessonType,
        CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            UPDATE MonthShedule
            SET Title = @title, DisciplineId = @did, TeacherId = @tid,
                GroupId = @gid, LessonTimeId = @ltid, Date = @date,
                Room = @room, LessonType = @type, UpdatedAt = NOW()
            WHERE MonthScheduleId = @id",
            c => c.Bind("@id", monthScheduleId)
                  .Bind("@title", title)
                  .Bind("@did", disciplineId)
                  .Bind("@tid", teacherId)
                  .Bind("@gid", groupId)
                  .Bind("@ltid", lessonTimeId)
                  .Bind("@date", date.ToDateTime(TimeOnly.MinValue))
                  .Bind("@room", room)
                  .Bind("@type", lessonType), ct);
    }

    public async Task CancelScheduleAsync(int monthScheduleId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(
            "UPDATE MonthShedule SET DeletedAt = NOW() WHERE MonthScheduleId = @id",
            c => c.Bind("@id", monthScheduleId), ct);

        await conn.ExecuteAsync(@"
            UPDATE Schedule SET Status = 'Cancelled', UpdatedAt = NOW()
            WHERE MonthScheduleId = @id",
            c => c.Bind("@id", monthScheduleId), ct);

        await tx.CommitAsync(ct);
    }

    public async Task<bool> HasConflictAsync(
    int groupId, int teacherId, int lessonTimeId, DateOnly date,
    int? excludeId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
        SELECT COUNT(*)
        FROM MonthShedule
        WHERE Date = @date::date
          AND LessonTimeId = @ltid::int
          AND DeletedAt IS NULL
          AND (GroupId = @gid::int OR TeacherId = @tid::int)
          AND (@excludeId::int IS NULL OR MonthScheduleId <> @excludeId::int)";
        var count = await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@date", date.ToDateTime(TimeOnly.MinValue))
            .Bind("@ltid", lessonTimeId)
            .Bind("@gid", groupId)
            .Bind("@tid", teacherId)
            .Bind("@excludeId", excludeId), ct);
        return count > 0;
    }

    private static ScheduleItem Map(System.Data.Common.DbDataReader r) => new(
        r.GetInt32(0),
        r.GetString(1),
        DateOnly.FromDateTime(r.GetDateTime(2)),
        r.GetInt32(3),
        (byte)r.GetInt16(4),
        TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(5)),
        TimeOnly.FromTimeSpan(r.GetFieldValue<TimeSpan>(6)),
        r.GetInt32(7),
        r.GetString(8),
        r.GetInt32(9),
        r.GetString(10),
        r.GetInt32(11),
        r.GetString(12),
        r.IsDBNull(13) ? null : r.GetString(13),
        r.GetString(14),
        r.IsDBNull(15) ? null : r.GetInt32(15),
        r.IsDBNull(16) ? null : r.GetString(16));
}