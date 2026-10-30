using Dapper;
using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class GradeHistoryRepository : IGradeHistoryRepository
{
    private readonly IDbConnectionFactory _factory;
    public GradeHistoryRepository(IDbConnectionFactory factory) => _factory = factory;
        
    public async Task<IReadOnlyList<GradeHistoryItem>> ListForStudentAsync(
        int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT h.UpdatedAt::date::text AS Date,
                   d.Title AS Discipline,
                   g.GradeValue,
                   'Домашняя' AS WorkType,
                   h.TeacherComment AS Comment
            FROM Homeworks h
            JOIN Grades g ON g.GradeId = h.GradeId
            JOIN Disciplines d ON d.DisciplineId = h.DisciplineId
            WHERE h.StudentId = @sid
              AND h.GradeId IS NOT NULL
              AND h.DeletedAt IS NULL

            UNION ALL

            SELECT ms.Date::text AS Date,
                   d.Title AS Discipline,
                   gr.GradeValue,
                   'Работа на паре' AS WorkType,
                   sg.Comment
            FROM ScheduleGrade sg
            JOIN Schedule s       ON s.ScheduleId = sg.ScheduleId
            JOIN MonthShedule ms  ON ms.MonthScheduleId = s.MonthScheduleId
            JOIN Disciplines d    ON d.DisciplineId = ms.DisciplineId
            JOIN Grades gr        ON gr.GradeId = sg.GradeId
            WHERE sg.StudentId = @sid

            ORDER BY Date DESC
            LIMIT 200";

        return await conn.QueryAsync(sql, r => new GradeHistoryItem(
            r.GetString(0),
            r.GetString(1),
            r.GetInt32(2),
            r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4)),
            c => c.Bind("@sid", studentId), ct);
    }

    public async Task<GradeHistorySummary> GetSummaryAsync(
        int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        const string avgSql = @"
            SELECT AVG(grade)::numeric(3,2)
            FROM (
                SELECT g.GradeValue AS grade
                FROM Homeworks h
                JOIN Grades g ON g.GradeId = h.GradeId
                WHERE h.StudentId = @sid
                  AND h.GradeId IS NOT NULL
                  AND h.DeletedAt IS NULL

                UNION ALL

                SELECT gr.GradeValue AS grade
                FROM ScheduleGrade sg
                JOIN Grades gr ON gr.GradeId = sg.GradeId
                WHERE sg.StudentId = @sid
            ) t";

        var avg = await conn.ExecuteScalarAsync<decimal?>(avgSql,
            c => c.Bind("@sid", studentId), ct);

        const string byDiscSql = @"
            SELECT discipline, AVG(grade)::numeric(3,2) AS avg, COUNT(*) AS cnt
            FROM (
                SELECT d.Title AS discipline, g.GradeValue AS grade
                FROM Homeworks h
                JOIN Grades g ON g.GradeId = h.GradeId
                JOIN Disciplines d ON d.DisciplineId = h.DisciplineId
                WHERE h.StudentId = @sid
                  AND h.GradeId IS NOT NULL
                  AND h.DeletedAt IS NULL

                UNION ALL

                SELECT d.Title AS discipline, gr.GradeValue AS grade
                FROM ScheduleGrade sg
                JOIN Schedule s       ON s.ScheduleId = sg.ScheduleId
                JOIN MonthShedule ms  ON ms.MonthScheduleId = s.MonthScheduleId
                JOIN Disciplines d    ON d.DisciplineId = ms.DisciplineId
                JOIN Grades gr        ON gr.GradeId = sg.GradeId
                WHERE sg.StudentId = @sid
            ) t
            GROUP BY discipline
            ORDER BY discipline";

        var byDisc = (await conn.QueryAsync(byDiscSql, r => new DisciplineAverage(
            r.GetString(0),
            r.GetDecimal(1),
            r.GetInt32(2)),
            c => c.Bind("@sid", studentId), ct)).ToList();

        return new GradeHistorySummary(avg, byDisc);
    }
}