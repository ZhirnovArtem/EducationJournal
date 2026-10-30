using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class DisciplinesEndpoints
{
    public static IEndpointRouteBuilder MapDisciplines(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/disciplines")
                   .WithTags("Disciplines")
                   .RequireAuthorization();

        g.MapGet("/", async Task<IResult> (
            int? page, int? pageSize,
            IDisciplineRepository disciplines,
            CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 50 : pageSize.Value;

            var items = await disciplines.ListAsync(p, ps, ct);
            var total = await disciplines.CountAsync(ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        });

        g.MapPost("/", async Task<IResult> (
            CreateDisciplineRequest req,
            IDisciplineRepository disciplines,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                throw new BusinessException("Название предмета обязательно");

            var id = await disciplines.InsertAsync(req.Title.Trim(), ct);
            return Results.Ok(new { disciplineId = id });
        })
        .RequireAuthorization("Admin");

        g.MapPut("/{id:int}", async Task<IResult> (
            int id,
            UpdateDisciplineRequest req,
            IDisciplineRepository disciplines,
            CancellationToken ct) =>
        {
            var existing = await disciplines.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Предмет не найден");

            if (string.IsNullOrWhiteSpace(req.Title))
                throw new BusinessException("Название предмета обязательно");

            await disciplines.UpdateAsync(id, req.Title.Trim(), ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin");

        g.MapDelete("/{id:int}", async Task<IResult> (
            int id,
            IDisciplineRepository disciplines,
            CancellationToken ct) =>
        {
            await disciplines.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin");

        return app;
    }

    public record CreateDisciplineRequest(string Title);
    public record UpdateDisciplineRequest(string Title);
}