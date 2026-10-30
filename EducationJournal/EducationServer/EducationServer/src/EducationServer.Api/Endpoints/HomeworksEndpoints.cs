using EducationServer.Api.Auth;
using EducationServer.Api.Hubs;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace EducationServer.Api.Endpoints;

public static class HomeworksEndpoints
{
    public static IEndpointRouteBuilder MapHomeworks(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/homeworks")
                   .WithTags("Homeworks")
                   .RequireAuthorization();

        // ──────────────────────────────────────────────
        // GET /api/v1/homeworks — список
        // ──────────────────────────────────────────────
        g.MapGet("/", async Task<IResult> (
            int? studentId,
            int? disciplineId,
            string? status,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            HomeworkService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 20 : pageSize.Value;

            var filter = new HomeworkFilterDto(
                studentId, disciplineId, status, from, to, p, ps);

            var (items, total) = await svc.ListAsync(
                filter, current.UserId, current.Role, ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        })
        .WithName("ListHomeworks")
        .WithSummary("Список домашних заданий (фильтры + пагинация)");

        // ──────────────────────────────────────────────
        // GET /api/v1/homeworks/submissions — сданные ДЗ для преподавателя
        // ВАЖНО: до /{id:int}, иначе catch-all перехватит
        // ──────────────────────────────────────────────
        g.MapGet("/submissions", async Task<IResult> (
            int? disciplineId,
            string? status,
            int? page,
            int? pageSize,
            HomeworkService service,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 50 : pageSize.Value;

            var (items, total) = await service.ListSubmissionsAsync(
                current.UserId, disciplineId, status, p, ps, ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("ListSubmissions")
        .WithSummary("Список сданных ДЗ (для проверки преподавателем)");

        // ──────────────────────────────────────────────
        // GET /api/v1/homeworks/{id} — карточка
        // ──────────────────────────────────────────────
        g.MapGet("/{id:int}", async Task<IResult> (
            int id,
            HomeworkService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, current.UserId, current.Role, ct);
            return Results.Ok(dto);
        })
        .WithName("GetHomework")
        .WithSummary("Карточка домашнего задания");

        // ──────────────────────────────────────────────
        // POST /api/v1/homeworks/batch — создать ДЗ для группы
        // ──────────────────────────────────────────────
        g.MapPost("/batch", async Task<IResult> (
            HttpRequest http,
            IHomeworkRepository repo,
            IFileRepository files,
            IFileStorage storage,
            CurrentUser current,
            IHubContext<NotificationHub> hub,
            CancellationToken ct) =>
        {
            if (current.Role is not ("Teacher" or "Admin"))
                throw new ForbiddenException();

            if (!http.HasFormContentType)
                throw new BusinessException("Ожидается multipart/form-data");

            var form = await http.ReadFormAsync(ct);

            if (!int.TryParse(form["groupId"].FirstOrDefault(), out var groupId) || groupId <= 0)
                throw new BusinessException("Группа обязательна");

            if (!int.TryParse(form["disciplineId"].FirstOrDefault(), out var disciplineId) || disciplineId <= 0)
                throw new BusinessException("Предмет обязателен");

            var task = form["task"].FirstOrDefault()?.Trim();
            var dateOfIssueRaw = form["dateOfIssue"].FirstOrDefault();
            var deadLineRaw = form["deadLine"].FirstOrDefault();

            if (!DateOnly.TryParse(dateOfIssueRaw, out var dateOfIssue))
                throw new BusinessException("Дата выдачи обязательна");

            if (!DateOnly.TryParse(deadLineRaw, out var deadLine))
                throw new BusinessException("Дедлайн обязателен");

            if (deadLine < dateOfIssue)
                throw new BusinessException("Дедлайн не может быть раньше даты выдачи");

            var file = form.Files.FirstOrDefault();

            var hasText = !string.IsNullOrWhiteSpace(task);
            var hasFile = file is { Length: > 0 };

            if (!hasText && !hasFile)
                throw new BusinessException("Добавьте текст задания или приложите файл");

            var teacherId = await repo.GetTeacherIdByUserIdAsync(current.UserId, ct)
                ?? throw new BusinessException("Текущий пользователь не является преподавателем");

            var studentIds = await repo.GetStudentIdsByGroupAsync(groupId, ct);
            if (studentIds.Count == 0)
                throw new BusinessException("В выбранной группе нет студентов");

            long? taskFileId = null;
            if (hasFile)
            {
                var saved = await storage.SaveAsync(
                    file!.OpenReadStream(), file.FileName, file.ContentType,
                    "edu-homeworks", ct);

                var entity = new EducationServer.Domain.FileEntity
                {
                    Bucket = saved.Bucket,
                    ObjectKey = saved.ObjectKey,
                    OriginalName = file.FileName,
                    MimeType = file.ContentType,
                    SizeBytes = saved.Size,
                    Checksum = saved.Sha256,
                    UploadedBy = current.UserId,
                };
                taskFileId = await files.InsertAsync(entity, ct);
            }

            var finalTask = hasText ? task! : "(см. вложение)";

            var created = await repo.CreateBatchAsync(
                teacherId, disciplineId, studentIds,
                finalTask, taskFileId, dateOfIssue, deadLine, ct);

            foreach (var sid in studentIds)
            {
                try
                {
                    await hub.Clients.Group($"student-{sid}").SendAsync(
                        "HomeworkAssigned",
                        new
                        {
                            task = finalTask,
                            hasFile,
                            deadLine = deadLine.ToString("yyyy-MM-dd"),
                        }, ct);
                }
                catch { }
            }

            return Results.Ok(new { created, students = studentIds.Count, hasFile });
        })
        .RequireAuthorization("TeacherOrAdmin")
        .DisableAntiforgery()
        .WithName("CreateHomeworkBatch")
        .WithSummary("Создать ДЗ сразу для всех студентов группы");

        // ──────────────────────────────────────────────
        // POST /api/v1/homeworks/{id}/submit — сдать ДЗ студентом
        // ──────────────────────────────────────────────
        g.MapPost("/{id:int}/submit", async Task<IResult> (
            int id,
            HttpRequest http,
            HomeworkService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
                throw new BusinessException("multipart/form-data required");

            var form = await http.ReadFormAsync(ct);
            var text = form["text"].FirstOrDefault();
            var files = form.Files.ToList();

            await svc.SubmitAsync(id, current.UserId, text, files, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Student")
        .DisableAntiforgery()
        .WithName("SubmitHomework")
        .WithSummary("Сдать домашнее задание (файлы упаковываются в ZIP)");

        // ──────────────────────────────────────────────
        // POST /api/v1/homeworks/{id}/grade — оценка преподавателем
        // ──────────────────────────────────────────────
        g.MapPost("/{id:int}/grade", async Task<IResult> (
            int id,
            GradeHomeworkRequest req,
            HomeworkService service,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            await service.GradeAsync(id, req.Grade, req.Comment, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("GradeHomework")
        .WithSummary("Выставить оценку и комментарий");

        // ──────────────────────────────────────────────
        // DELETE /api/v1/homeworks/{id} — мягкое удаление
        // ──────────────────────────────────────────────
        g.MapDelete("/{id:int}", async Task<IResult> (
            int id,
            HomeworkService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("DeleteHomework")
        .WithSummary("Мягкое удаление домашнего задания");

        return app;
    }

    // ──────────────────────────────────────────────
    // DTO — объявляются В КЛАССЕ, а не в методе
    // ──────────────────────────────────────────────
    public record GradeHomeworkRequest(int? Grade, string? Comment);
}