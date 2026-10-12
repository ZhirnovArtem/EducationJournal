using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/users")
                   .WithTags("Users")
                   .RequireAuthorization("Admin");

        g.MapGet("/", async Task<IResult> (
            int? page, int? pageSize,
            IUserRepository users, CancellationToken ct) =>
        {
            var p = page is null or <= 0 ? 1 : page.Value;
            var ps = pageSize is null or <= 0 or > 100 ? 20 : pageSize.Value;

            var items = await users.ListAsync(p, ps, ct);
            var total = await users.CountAsync(ct);

            return Results.Ok(new { items, total, page = p, pageSize = ps });
        });

        g.MapPost("/", async Task<IResult> (
            CreateUserRequest req,
            IUserRepository users, IPasswordHasher hasher,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Password))
                throw new BusinessException("Login and password required");

            var u = new EducationServer.Domain.User
            {
                Login = req.Login,
                Email = req.Email ?? req.Login + "@local",
                PasswordHash = hasher.Hash(req.Password),
                PasswordAlgo = "argon2id",
                RoleId = req.RoleId,
            };

            var id = await users.InsertAsync(u, ct);
            return Results.Ok(new { userId = id });
        });

        g.MapDelete("/{id:int}", async Task<IResult> (
            int id, IUserRepository users, CancellationToken ct) =>
        {
            await users.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        });

        g.MapPut("/{id:int}", async Task<IResult> (
    int id,
    UpdateUserRequest req,
    IUserRepository users,
    IPasswordHasher hasher,
    CancellationToken ct) =>
        {
            var existing = await users.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("User not found");
            await users.UpdateAsync(id, req.Email, req.RoleId, req.IsActive, ct);
            if (!string.IsNullOrWhiteSpace(req.NewPassword))
            {
                if (req.NewPassword.Length < 6)
                    throw new BusinessException("Пароль минимум 6 символов");

                var hash = hasher.Hash(req.NewPassword);
                await users.UpdatePasswordAsync(id, hash, ct);
            }
            return Results.NoContent();
        });


        return app;
    }

    public record CreateUserRequest(string Login, string Password, string? Email, int RoleId);
    public record UpdateUserRequest(string? Email,int RoleId,bool IsActive,string? NewPassword);
}