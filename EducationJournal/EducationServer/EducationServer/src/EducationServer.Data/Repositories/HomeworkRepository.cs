using System.Data.Common;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;

namespace EducationServer.Data.Repositories;

public sealed class HomeworkRepository : IHomeworkRepository
{
    private readonly IDbConnectionFactory _factory;
    public HomeworkRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<int> GetStudentIdByUserAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT StudentId FROM Students WHERE UserId = @uid AND DeletedAt IS NULL",
            c => c.Bind("@uid", userId), ct);
    }

    public async Task<(IReadOnlyList<HomeworkListItem> Items, int Total)> ListAsync(
        HomeworkQuery q, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        var where = new List<string> { "h.DeletedAt IS NULL" };
        if (q.StudentId.HasValue) where.Add("h.StudentId = @sid");
        if (q.DisciplineId.HasValue) where.Add("h.DisciplineId = @did");
        if (q.From.HasValue) where.Add("h.DateOfIssue >= @from");
        if (q.To.HasValue) where.Add("h.DateOfIssue <= @to");

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            switch (q.Status!.ToLowerInvariant())
            {
                case "pending": where.Add("h.DueDate IS NULL"); break;
                case "submitted": where.Add("h.DueDate IS NOT NULL AND h.GradeId IS NULL"); break;
                case "graded": where.Add("h.GradeId IS NOT NULL"); break;
                case "overdue": where.Add("h.DueDate IS NULL AND h.DeadLine < CURRENT_DATE"); break;
            }
        }

        var whereSql = "WHERE " + string.Join(" AND ", where);

        void BindFilter(DbCommand c)
        {
            if (q.StudentId.HasValue) c.Bind("@sid", q.StudentId.Value);
            if (q.DisciplineId.HasValue) c.Bind("@did", q.DisciplineId.Value);
            if (q.From.HasValue) c.Bind("@from", q.From.Value.ToDateTime(TimeOnly.MinValue));
            if (q.To.HasValue) c.Bind("@to", q.To.Value.ToDateTime(TimeOnly.MinValue));
        }

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Homeworks h {whereSql}",
            BindFilter, ct);

        var sql = $@"
            SELECT h.HomeworkId, h.Task,
                   d.Title,
                   t.LastName || ' ' || t.FirstName,
                   h.DateOfIssue, h.DeadLine, h.DueDate,
                   g.GradeValue,
                   h.TaskFileId, h.AnswerArchiveId
            FROM Homeworks h
            JOIN Disciplines d ON d.DisciplineId = h.DisciplineId
            JOIN Teachers t    ON t.TeacherId = h.TeacherId
            LEFT JOIN Grades g ON g.GradeId = h.GradeId
            {whereSql}
            ORDER BY h.DateOfIssue DESC
            OFFSET @offset LIMIT @limit";

        var items = await conn.QueryAsync(sql, r => new HomeworkListItem(
                r.GetInt32(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                DateOnly.FromDateTime(r.GetDateTime(4)),
                DateOnly.FromDateTime(r.GetDateTime(5)),
                r.IsDBNull(6) ? null : DateOnly.FromDateTime(r.GetDateTime(6)),
                r.IsDBNull(7) ? null : r.GetInt32(7),
                r.IsDBNull(8) ? null : r.GetInt64(8),
                r.IsDBNull(9) ? null : r.GetInt64(9)),
            c =>
            {
                BindFilter(c);
                c.Bind("@offset", (q.Page - 1) * q.PageSize);
                c.Bind("@limit", q.PageSize);
            }, ct);

        return (items, total);
    }

    public async Task<Homework?> FindByIdAsync(int id, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT HomeworkId, StudentId, TeacherId, DisciplineId, Task, TaskFileId,
                   HomeworkText, AnswerFileId, AnswerArchiveId,
                   DateOfIssue, DeadLine, DueDate, GradeId,
                   TeacherComment,
                   CreatedAt, UpdatedAt, DeletedAt
            FROM Homeworks
            WHERE HomeworkId = @id AND DeletedAt IS NULL";
        return await conn.QueryFirstAsync(sql, r => new Homework
        {
            HomeworkId = r.GetInt32(0),
            StudentId = r.GetInt32(1),
            TeacherId = r.GetInt32(2),
            DisciplineId = r.GetInt32(3),
            Task = r.GetString(4),
            TaskFileId = r.IsDBNull(5) ? null : r.GetInt64(5),
            HomeworkText = r.IsDBNull(6) ? null : r.GetString(6),
            AnswerFileId = r.IsDBNull(7) ? null : r.GetInt64(7),
            AnswerArchiveId = r.IsDBNull(8) ? null : r.GetInt64(8),
            DateOfIssue = DateOnly.FromDateTime(r.GetDateTime(9)),
            DeadLine = DateOnly.FromDateTime(r.GetDateTime(10)),
            DueDate = r.IsDBNull(11) ? null : DateOnly.FromDateTime(r.GetDateTime(11)),
            GradeId = r.IsDBNull(12) ? null : r.GetInt32(12),
            TeacherComment = r.IsDBNull(13) ? null : r.GetString(13),
            CreatedAt = r.GetDateTime(14),
            UpdatedAt = r.GetDateTime(15),
            DeletedAt = r.IsDBNull(16) ? null : r.GetDateTime(16),
        }, c => c.Bind("@id", id), ct);
    }

    public async Task<int> InsertAsync(Homework hw, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            INSERT INTO Homeworks
                (StudentId, TeacherId, DisciplineId, Task, TaskFileId,
                 DateOfIssue, DeadLine)
            VALUES
                (@sid, @tid, @did, @task, @tfile, @doi, @dl)
            RETURNING HomeworkId";
        return await conn.ExecuteScalarAsync<int>(sql, c => c
            .Bind("@sid", hw.StudentId)
            .Bind("@tid", hw.TeacherId)
            .Bind("@did", hw.DisciplineId)
            .Bind("@task", hw.Task)
            .Bind("@tfile", hw.TaskFileId)
            .Bind("@doi", hw.DateOfIssue.ToDateTime(TimeOnly.MinValue))
            .Bind("@dl", hw.DeadLine.ToDateTime(TimeOnly.MinValue)), ct);
    }

    public async Task UpdateSubmissionAsync(int homeworkId, string? text,
        long? answerFileId, long? answerArchiveId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            UPDATE Homeworks
            SET HomeworkText = @text,
                AnswerFileId = @file,
                AnswerArchiveId = @arch,
                DueDate = CURRENT_DATE,
                UpdatedAt = NOW()
            WHERE HomeworkId = @id";
        await conn.ExecuteAsync(sql, c => c
            .Bind("@text", text)
            .Bind("@file", answerFileId)
            .Bind("@arch", answerArchiveId)
            .Bind("@id", homeworkId), ct);
    }

    public async Task UpdateGradeAsync(int homeworkId, int? gradeId, string? comment, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE Homeworks 
          SET GradeId = @g, TeacherComment = @c, UpdatedAt = NOW()
          WHERE HomeworkId = @id",
            c => c.Bind("@id", homeworkId)
                  .Bind("@g", gradeId)
                  .Bind("@c", comment),
            ct);
    }

    public async Task<int> AutoGradeOverdueAsync(int studentId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        var kollId = await conn.ExecuteScalarAsync<int?>(
            "SELECT GradeId FROM Grades WHERE GradeValue = 1 LIMIT 1",
            c => { }, ct);

        if (kollId is null)
            return 0;  
        const string sql = @"
        UPDATE Homeworks
        SET GradeId = @g,
            TeacherComment = COALESCE(TeacherComment, 'Автоматически: просрочен дедлайн'),
            UpdatedAt = NOW()
        WHERE StudentId = @sid
          AND DeletedAt IS NULL
          AND GradeId IS NULL
          AND DueDate IS NULL
          AND DeadLine < CURRENT_DATE";
        return await conn.ExecuteAsync(sql, c => c
            .Bind("@g", kollId.Value)
            .Bind("@sid", studentId), ct);
    }

    public async Task SoftDeleteAsync(int homeworkId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE Homeworks SET DeletedAt = NOW() WHERE HomeworkId = @id",
            c => c.Bind("@id", homeworkId), ct);
    }

    public async Task<long?> GetArchiveIdAsync(int homeworkId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long?>(
            "SELECT AnswerArchiveId FROM Homeworks WHERE HomeworkId = @id",
            c => c.Bind("@id", homeworkId), ct);
    }

    public async Task<int?> GetTeacherIdByUserIdAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int?>(
            "SELECT TeacherId FROM Teachers WHERE UserId = @uid AND DeletedAt IS NULL",
            c => c.Bind("@uid", userId), ct);
    }

    public async Task<IReadOnlyList<int>> GetStudentIdsByGroupAsync(int groupId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
        SELECT StudentId
        FROM Students
        WHERE GroupId = @gid AND DeletedAt IS NULL
        ORDER BY StudentId";
        return await conn.QueryAsync(sql,
            r => r.GetInt32(0),
            c => c.Bind("@gid", groupId), ct);
    }

    public async Task<(IReadOnlyList<SubmissionListItem> Items, int Total)> ListSubmissionsAsync(
    int teacherUserId,
    int? disciplineId,
    string? status,
    int page,
    int pageSize,
    CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        // Определяем teacherId по userId
        var teacherId = await GetTeacherIdByUserIdAsync(teacherUserId, ct);
        if (teacherId is null)
            return (Array.Empty<SubmissionListItem>(), 0);

        var where = new List<string>
    {
        "h.DeletedAt IS NULL",
        "h.TeacherId = @tid",
        "h.DueDate IS NOT NULL"    // только сданные
    };

        if (disciplineId.HasValue) where.Add("h.DisciplineId = @did");

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status!.ToLowerInvariant())
            {
                case "pending": where.Add("h.GradeId IS NULL"); break;
                case "graded": where.Add("h.GradeId IS NOT NULL"); break;
            }
        }

        var whereSql = "WHERE " + string.Join(" AND ", where);

        void BindFilter(DbCommand c)
        {
            c.Bind("@tid", teacherId.Value);
            if (disciplineId.HasValue) c.Bind("@did", disciplineId.Value);
        }

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Homeworks h {whereSql}",
            BindFilter, ct);

        var sql = $@"
        SELECT h.HomeworkId,
               h.StudentId,
               s.LastName || ' ' || s.FirstName AS StudentName,
               h.Task,
               d.Title AS Discipline,
               h.DeadLine,
               h.DueDate,
               g.GradeValue,
               h.TeacherComment,
               h.AnswerFileId,
               h.AnswerArchiveId
        FROM Homeworks h
        JOIN Students s   ON s.StudentId = h.StudentId
        JOIN Disciplines d ON d.DisciplineId = h.DisciplineId
        LEFT JOIN Grades g ON g.GradeId = h.GradeId
        {whereSql}
        ORDER BY h.DueDate DESC
        OFFSET @offset LIMIT @limit";

        var items = await conn.QueryAsync(sql, r => new SubmissionListItem(
                r.GetInt32(0),                        // HomeworkId
                r.GetInt32(1),                        // StudentId
                r.GetString(2),                       // StudentName
                r.GetString(3),                       // Task
                r.GetString(4),                       // Discipline
                DateOnly.FromDateTime(r.GetDateTime(5)),   // DeadLine
                r.IsDBNull(6) ? null : DateOnly.FromDateTime(r.GetDateTime(6)),   // DueDate
                r.IsDBNull(7) ? null : r.GetInt32(7),      // Grade
                r.IsDBNull(8) ? null : r.GetString(8),     // TeacherComment
                r.IsDBNull(9) ? null : r.GetInt64(9),      // AnswerFileId
                r.IsDBNull(10) ? null : r.GetInt64(10)),   // AnswerArchiveId
            c =>
            {
                BindFilter(c);
                c.Bind("@offset", (page - 1) * pageSize);
                c.Bind("@limit", pageSize);
            }, ct);

        return (items, total);
    }

    public async Task<int> CreateBatchAsync(
        int teacherId,
        int disciplineId,
        IReadOnlyList<int> studentIds,
        string task,
        long? taskFileId,
        DateOnly dateOfIssue,
        DateOnly deadLine,
        CancellationToken ct)
    {
        if (studentIds.Count == 0) return 0;

        await using var conn = await _factory.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string sql = @"
        INSERT INTO Homeworks
            (StudentId, TeacherId, DisciplineId, Task, TaskFileId, DateOfIssue, DeadLine)
        VALUES
            (@sid, @tid, @did, @task, @tfid, @doi, @dl)";

        var created = 0;
        foreach (var sid in studentIds.Distinct())
        {
            await conn.ExecuteAsync(sql, c => c
                .Bind("@sid", sid)
                .Bind("@tid", teacherId)
                .Bind("@did", disciplineId)
                .Bind("@task", task)
                .Bind("@tfid", taskFileId)
                .Bind("@doi", dateOfIssue.ToDateTime(TimeOnly.MinValue))
                .Bind("@dl", deadLine.ToDateTime(TimeOnly.MinValue)), ct);
            created++;
        }

        await tx.CommitAsync(ct);
        return created;
    }

}