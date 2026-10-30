using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Api.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using EducationServer.Domain;
using System.Linq;
using Dapper; // Обязательно для работы методов ExecuteAsync и QueryAsync
using System.Data; // Для IDbConnection

namespace EducationServer.Api.Endpoints;

public static class IdeEndpoints
{
    public static IEndpointRouteBuilder MapIde(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/ide")
                   .WithTags("IDE")
                   .RequireAuthorization();

        // 1. Эндпоинт для отправки кода (Студент)
        g.MapPost("/submit", async Task<IResult> (
            IdeSubmitRequest req,
            CurrentUser current,
            IDbConnectionFactory dbFactory,
            IHubContext<NotificationHub> hubContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                throw new BusinessException("Код не может быть пустым");

            await using var conn = await dbFactory.OpenAsync(ct);

            const string sql = @"
                INSERT INTO IdeSubmissions (Id, StudentLogin, Code, Language, SubmittedAt, TeacherId)
                VALUES (@Id, @StudentLogin, @Code, @Language, @SubmittedAt, @TeacherId)";

            var submission = new
            {
                Id = Guid.NewGuid(),
                StudentLogin = current.Login,
                Code = req.Code,
                Language = req.Language ?? "python",
                SubmittedAt = DateTime.UtcNow,
                TeacherId = req.TeacherId
            };

            // Чистый Dapper: передаем объект, он сам подставит значения
            await conn.ExecuteAsync(sql, submission);

            var notification = new
            {
                title = "Новое решение из IDE",
                text = $"Студент {current.Login} отправил код на проверку",
                url = "#ide-submissions"
            };

            await hubContext.Clients.Group("role-teacher")
                .SendAsync("ReceiveNotification", notification, ct);

            return Results.Ok(new { message = "Код успешно отправлен преподавателю" });
        });

        // 2. Эндпоинт для получения списка кодов
        g.MapGet("/submissions", async Task<IResult> (
            CurrentUser current,
            IDbConnectionFactory dbFactory,
            CancellationToken ct) =>
        {
            await using var conn = await dbFactory.OpenAsync(ct);

            const string sql = @"
                SELECT Id, StudentLogin, Code, Language, SubmittedAt, TeacherId 
                FROM IdeSubmissions 
                ORDER BY SubmittedAt DESC";

            // Чистый Dapper: возвращаем список анонимных объектов
            var submissions = await conn.QueryAsync(sql);

            return Results.Ok(new { items = submissions });
        });

        // 3. Эндпоинт для получения преподавателей
        g.MapGet("/teachers", async Task<IResult> (
            CurrentUser current,
            ITeacherRepository teacherRepo,
            CancellationToken ct) =>
        {
            if (current.Role != "student")
                return Results.Ok(new { items = new List<object>() });

            var teachers = await teacherRepo.ListAsync(1, 100, ct);

            var result = teachers.Select(t => new
            {
                teacherId = t.TeacherId,
                fullName = $"{t.LastName} {t.FirstName} {t.Patronymic}".Trim()
            }).ToList();

            return Results.Ok(new { items = result });
        });

        return app;
    }

    public record IdeSubmitRequest(string Code, string Language, int? TeacherId);
}