using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapSchedule(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/schedule")
                   .WithTags("Schedule")
                   .RequireAuthorization();

        g.MapGet("/", async Task<IResult> (
            DateOnly? from, DateOnly? to,
            int? groupId, int? teacherId,
            IScheduleRepository schedule,
            CurrentUser current,
            CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var f = from ?? new DateOnly(today.Year, today.Month, 1);
            var t = to ?? f.AddMonths(1).AddDays(-1);

            var items = await schedule.GetRangeAsync(f, t, groupId, teacherId, ct);
            return Results.Ok(items);
        });

        g.MapGet("/lesson-times", async Task<IResult> (
            IScheduleRepository schedule, CancellationToken ct) =>
        {
            var items = await schedule.GetLessonTimesAsync(ct);
            return Results.Ok(items);
        });

        g.MapGet("/{id:int}", async Task<IResult> (
            int id, IScheduleRepository schedule, CancellationToken ct) =>
        {
            var item = await schedule.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Занятие не найдено");
            return Results.Ok(item);
        });

        // ── СОЗДАНИЕ ПАРЫ ──
        g.MapPost("/", async Task<IResult> (
            CreateScheduleRequest req,
            IScheduleRepository schedule,
            ITeacherRepository teachers,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                throw new BusinessException("Название пары обязательно");

            if (req.DisciplineId <= 0 || req.TeacherId <= 0 || req.GroupId <= 0)
                throw new BusinessException("Предмет, преподаватель и группа обязательны");

            // ── ПРОВЕРКА ПРАВ ──
            if (current.IsTeacher)
            {
                var myTeacher = await teachers.FindByUserIdAsync(current.UserId, ct);
                if (myTeacher == null || myTeacher.TeacherId != req.TeacherId)
                {
                    throw new BusinessException("Вы можете создавать пары только для себя");
                }
            }

            var conflict = await schedule.HasConflictAsync(
                req.GroupId, req.TeacherId, req.LessonTimeId, req.Date, null, ct);
            if (conflict)
                throw new ConflictException("На это время у группы или преподавателя уже стоит пара");

            var id = await schedule.CreateMonthScheduleAsync(
                req.Title.Trim(), req.DisciplineId, req.TeacherId, req.GroupId,
                req.LessonTimeId, req.Date,
                req.Room?.Trim(),
                string.IsNullOrWhiteSpace(req.LessonType) ? "Lecture" : req.LessonType!,
                ct);

            return Results.Ok(new { monthScheduleId = id });
        })
        .RequireAuthorization("TeacherOrAdmin");

        // ── РЕДАКТИРОВАНИЕ ПАРЫ ──
        g.MapPut("/{id:int}", async Task<IResult> (
            int id, UpdateScheduleRequest req,
            IScheduleRepository schedule,
            ITeacherRepository teachers,
            CurrentUser current,
            CancellationToken ct) =>
        {
            var existing = await schedule.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Занятие не найдено");

            // ── ПРОВЕРКА ПРАВ ──
            if (current.IsTeacher)
            {
                var myTeacher = await teachers.FindByUserIdAsync(current.UserId, ct);
                if (myTeacher == null)
                    throw new BusinessException("Профиль преподавателя не найден");

                if (existing.TeacherId != myTeacher.TeacherId)
                    throw new BusinessException("Вы можете редактировать только свои пары");

                if (myTeacher.TeacherId != req.TeacherId)
                    throw new BusinessException("Вы не можете назначить пару другому преподавателю");
            }

            var conflict = await schedule.HasConflictAsync(
                req.GroupId, req.TeacherId, req.LessonTimeId, req.Date, id, ct);
            if (conflict)
                throw new ConflictException("На это время уже стоит другая пара");

            await schedule.UpdateMonthScheduleAsync(
                id, req.Title.Trim(), req.DisciplineId, req.TeacherId, req.GroupId,
                req.LessonTimeId, req.Date, req.Room?.Trim(),
                string.IsNullOrWhiteSpace(req.LessonType) ? "Lecture" : req.LessonType!,
                ct);

            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin");

        // ── УДАЛЕНИЕ ПАРЫ ──
        g.MapDelete("/{id:int}", async Task<IResult> (
            int id,
            IScheduleRepository schedule,
            ITeacherRepository teachers,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (current.IsTeacher)
            {
                var existing = await schedule.FindByIdAsync(id, ct)
                    ?? throw new NotFoundException("Занятие не найдено");

                var myTeacher = await teachers.FindByUserIdAsync(current.UserId, ct);
                if (myTeacher == null || existing.TeacherId != myTeacher.TeacherId)
                {
                    throw new BusinessException("Вы можете отменять только свои пары");
                }
            }

            await schedule.CancelScheduleAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin");

        return app;
    }

    public record CreateScheduleRequest(
        string Title, int DisciplineId, int TeacherId, int GroupId,
        int LessonTimeId, DateOnly Date, string? Room, string? LessonType);

    public record UpdateScheduleRequest(
        string Title, int DisciplineId, int TeacherId, int GroupId,
        int LessonTimeId, DateOnly Date, string? Room, string? LessonType);
}