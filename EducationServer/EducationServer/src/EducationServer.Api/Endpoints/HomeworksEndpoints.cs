using EducationServer.Api.Auth;
using EducationServer.Domain;
using EducationServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class HomeworksEndpoints
{
    public static IEndpointRouteBuilder MapHomeworks(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/homeworks")
                   .WithTags("Homeworks")
                   .RequireAuthorization();
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

            return Results.Ok(new
            {
                items,
                total,
                page = p,
                pageSize = ps,
            });
        })
        .WithName("ListHomeworks")
        .WithSummary("Список домашних заданий (фильтры + пагинация)")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);
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
        .WithSummary("Карточка домашнего задания")
        .Produces<HomeworkDetailsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden);
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
        .WithSummary("Сдать домашнее задание (файлы упаковываются в ZIP)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
        g.MapPost("/{id:int}/grade", async Task<IResult> (
            int id,
            GradeRequest req,
            HomeworkService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            if (req.Grade is < 1 or > 5)
                throw new BusinessException("Grade must be 1..5");

            await svc.GradeAsync(id, req.Grade, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("GradeHomework")
        .WithSummary("Оценить домашнее задание (1–5)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
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
        .WithSummary("Мягкое удаление домашнего задания")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public record GradeRequest(int Grade, string? Comment);
}