using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class GradeSheetRepository : IGradeSheetRepository
{
	private readonly IDbConnectionFactory _factory;
	public GradeSheetRepository(IDbConnectionFactory factory) => _factory = factory;

	public async Task<IReadOnlyList<GradeOption>> GetGradesAsync(CancellationToken ct)
	{
		await using var conn = await _factory.OpenAsync(ct);
		const string sql = @"
            SELECT GradeId, GradeValue
            FROM Grades
            ORDER BY GradeValue";
		return await conn.QueryAsync(sql, r => new GradeOption(
			r.GetInt32(0),
			r.GetInt32(1)), null, ct);
	}

	public async Task<GradeSheet?> GetSheetAsync(int monthScheduleId, CancellationToken ct)
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
                   sg.GradeId, gr.GradeValue, sg.Comment
            FROM Students st
            LEFT JOIN ScheduleGrade sg
                ON sg.StudentId = st.StudentId AND sg.ScheduleId = @sid
            LEFT JOIN Grades gr ON gr.GradeId = sg.GradeId
            WHERE st.GroupId = @gid AND st.DeletedAt IS NULL
            ORDER BY st.LastName, st.FirstName";

		var students = await conn.QueryAsync(studentsSql, r => new StudentGradeRow(
			r.GetInt32(0),
			r.GetString(1),
			r.GetString(2),
			r.IsDBNull(3) ? null : r.GetString(3),
			r.IsDBNull(4) ? null : r.GetInt32(4),
			r.IsDBNull(5) ? null : r.GetInt32(5),
			r.IsDBNull(6) ? null : r.GetString(6)),
			c => c.Bind("@sid", head.ScheduleId).Bind("@gid", head.GroupId), ct);

		var grades = await GetGradesAsync(ct);

		return new GradeSheet(
			head.ScheduleId,
			monthScheduleId,
			head.Date,
			head.DisciplineTitle,
			head.GroupTitle,
			head.PairNumber,
			head.StartTime,
			head.EndTime,
			students,
			grades);
	}

	public async Task SaveAsync(
		int scheduleId,
		int markedByUserId,
		IReadOnlyList<(int StudentId, int GradeId, string? Comment)> marks,
		CancellationToken ct)
	{
		await using var conn = await _factory.OpenAsync(ct);
		await using var tx = await conn.BeginTransactionAsync(ct);

		// Удаляем все старые оценки этой пары и вставляем заново
		await conn.ExecuteAsync(
			"DELETE FROM ScheduleGrade WHERE ScheduleId = @sid",
			c => c.Bind("@sid", scheduleId), ct);

		const string insSql = @"
            INSERT INTO ScheduleGrade
                (ScheduleId, StudentId, GradeId, Comment, MarkedBy, MarkedAt)
            VALUES
                (@sid, @stid, @gid, @comment, @by, NOW())";

		foreach (var m in marks)
		{
			await conn.ExecuteAsync(insSql, c => c
				.Bind("@sid", scheduleId)
				.Bind("@stid", m.StudentId)
				.Bind("@gid", m.GradeId)
				.Bind("@comment", m.Comment)
				.Bind("@by", markedByUserId), ct);
		}

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