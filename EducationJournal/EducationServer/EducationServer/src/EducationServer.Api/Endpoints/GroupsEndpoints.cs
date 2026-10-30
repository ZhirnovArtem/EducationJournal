using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class GroupsEndpoints
{
    public static IEndpointRouteBuilder MapGroups(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/groups")
                   .WithTags("Groups")
                   .RequireAuthorization();

        // GET /api/v1/groups — читают все авторизованные
        g.MapGet("/", async Task<IResult> (
            int? page, int? pageSize,
            IGroupRepository groups,
            CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 50 : pageSize.Value;

            var items = await groups.ListAsync(p, ps, ct);
            var total = await groups.CountAsync(ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        });

        // POST /api/v1/groups — только Admin
        g.MapPost("/", async Task<IResult> (
            CreateGroupRequest req,
            IGroupRepository groups,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                throw new BusinessException("Название класса обязательно");

            var id = await groups.InsertAsync(req.Title.Trim(), ct);
            return Results.Ok(new { groupId = id });
        })
        .RequireAuthorization("Admin");

        // PUT /api/v1/groups/{id} — только Admin
        g.MapPut("/{id:int}", async Task<IResult> (
            int id,
            UpdateGroupRequest req,
            IGroupRepository groups,
            CancellationToken ct) =>
        {
            var existing = await groups.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("Класс не найден");

            if (string.IsNullOrWhiteSpace(req.Title))
                throw new BusinessException("Название класса обязательно");

            await groups.UpdateAsync(id, req.Title.Trim(), ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin");

        // DELETE /api/v1/groups/{id} — только Admin
        g.MapDelete("/{id:int}", async Task<IResult> (
            int id,
            IGroupRepository groups,
            CancellationToken ct) =>
        {
            await groups.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("Admin");

        return app;
    }

    // ↓↓↓ ОДНО объявление каждого record, в конце класса ↓↓↓
    public record CreateGroupRequest(string Title);
    public record UpdateGroupRequest(string Title);
}