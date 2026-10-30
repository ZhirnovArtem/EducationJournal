using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class StudentsEndpoints
{
    public static IEndpointRouteBuilder MapStudents(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/students")
                   .WithTags("Students")
                   .RequireAuthorization("Admin");

        g.MapGet("/", async Task<IResult> (
            int? page, int? pageSize,
            IStudentRepository students, CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 50 : pageSize.Value;

            var items = await students.ListAsync(p, ps, ct);
            var total = await students.CountAsync(ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        });

        g.MapGet("/{id:int}", async Task<IResult> (
            int id, IStudentRepository students, CancellationToken ct) =>
        {
            var s = await students.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Студент не найден");
            return Results.Ok(s);
        });

        g.MapPut("/{id:int}", async Task<IResult> (
            int id, UpdateStudentRequest req,
            IStudentRepository students, CancellationToken ct) =>
        {
            var existing = await students.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Студент не найден");

            await students.UpdateAsync(id,
                req.FirstName, req.LastName, req.Patronymic,
                req.Email, req.GroupId, ct);

            return Results.NoContent();
        });

        g.MapPut("/{id:int}/group", async Task<IResult> (
            int id, UpdateStudentGroupRequest req,
            IStudentRepository students, CancellationToken ct) =>
        {
            var existing = await students.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Студент не найден");

            await students.UpdateGroupAsync(id, req.GroupId, ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:int}", async Task<IResult> (
            int id, IStudentRepository students, CancellationToken ct) =>
        {
            await students.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }

    public record UpdateStudentRequest(
        string FirstName, string LastName, string? Patronymic,
        string Email, int? GroupId);

    public record UpdateStudentGroupRequest(int? GroupId);
}