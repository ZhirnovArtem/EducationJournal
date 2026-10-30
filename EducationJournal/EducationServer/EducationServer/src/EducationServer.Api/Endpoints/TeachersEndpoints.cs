using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Api.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class TeachersEndpoints
{
    public static IEndpointRouteBuilder MapTeachers(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/teachers")
                   .WithTags("Teachers")
                   .RequireAuthorization(); // <--- Разрешаем всем авторизованным (и студентам, и преподам)

        // 1. Список преподавателей (GET /)
        g.MapGet("/", async Task<IResult> (
            int? page, int? pageSize,
            ITeacherRepository teachers, CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 50 : pageSize.Value;

            var items = await teachers.ListAsync(p, ps, ct);
            var total = await teachers.CountAsync(ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        });

        // 2. Получить преподавателя по ID (GET /{id})
        g.MapGet("/{id:int}", async Task<IResult> (
            int id, ITeacherRepository teachers, CancellationToken ct) =>
        {
            var t = await teachers.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Преподаватель не найден");
            return Results.Ok(t);
        });

        // 3. Свой профиль (GET /me) - только для преподавателей
        g.MapGet("/me", async Task<IResult> (
            ITeacherRepository teachers,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher) return Results.Forbid();

            var myProfile = await teachers.FindByUserIdAsync(current.UserId, ct);
            if (myProfile == null) return Results.NotFound();

            return Results.Ok(myProfile);
        });

        // ── НИЖЕ ТОЛЬКО ДЛЯ АДМИНА ──

        // 4. Обновить преподавателя (PUT /{id})
        g.MapPut("/{id:int}", async Task<IResult> (
            int id, UpdateTeacherRequest req,
            ITeacherRepository teachers, CancellationToken ct) =>
        {
            var existing = await teachers.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Преподаватель не найден");

            await teachers.UpdateAsync(id,
                req.FirstName, req.LastName, req.Patronymic, req.Email, ct);

            return Results.NoContent();
        })
        .RequireAuthorization("Admin"); // <--- Только для админа

        // 5. Обновить предметы преподавателя (PUT /{id}/disciplines)
        g.MapPut("/{id:int}/disciplines", async Task<IResult> (
            int id, UpdateTeacherDisciplinesRequest req,
            ITeacherRepository teachers, CancellationToken ct) =>
        {
            var existing = await teachers.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Преподаватель не найден");

            await teachers.SetDisciplinesAsync(id, req.DisciplineIds ?? new List<int>(), ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin"); // <--- Только для админа

        // 6. Удалить преподавателя (DELETE /{id})
        g.MapDelete("/{id:int}", async Task<IResult> (
            int id, ITeacherRepository teachers, CancellationToken ct) =>
        {
            await teachers.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin"); // <--- Только для админа

        return app;
    }

    public record UpdateTeacherRequest(
        string FirstName, string LastName, string? Patronymic, string Email);

    public record UpdateTeacherDisciplinesRequest(List<int>? DisciplineIds);
}