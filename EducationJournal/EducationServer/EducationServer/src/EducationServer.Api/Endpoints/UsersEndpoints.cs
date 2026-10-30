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
    IUserRepository users,
    IStudentRepository students,
    ITeacherRepository teachers,
    IPasswordHasher hasher,
    IDbConnectionFactory factory,
    CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Password))
                throw new BusinessException("Логин и пароль обязательны");
            if (req.RoleId is 2 or 3)
            {
                if (string.IsNullOrWhiteSpace(req.FirstName))
                    throw new BusinessException("Имя обязательно");
                if (string.IsNullOrWhiteSpace(req.LastName))
                    throw new BusinessException("Фамилия обязательна");
            }

            if (req.RoleId == 3 && req.BirthDate is null)
                throw new BusinessException("Дата рождения обязательна для студента");
            await using var conn = await factory.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                var u = new EducationServer.Domain.User
                {
                    Login = req.Login.Trim(),
                    Email = req.Email?.Trim() ?? req.Login.Trim() + "@local",
                    PasswordHash = hasher.Hash(req.Password),
                    PasswordAlgo = "argon2id",
                    RoleId = req.RoleId,
                };
                var userId = await users.InsertAsync(u, ct);
                if (req.RoleId == 2)
                {
                    await teachers.InsertAsync(userId,
                        req.FirstName!.Trim(),
                        req.LastName!.Trim(),
                        req.Patronymic?.Trim(),
                        u.Email, ct);
                }
                else if (req.RoleId == 3)
                {
                    await students.InsertAsync(userId,
                        req.FirstName!.Trim(),
                        req.LastName!.Trim(),
                        req.Patronymic?.Trim(),
                        req.BirthDate!.Value,
                        u.Email,
                        null, ct);
                }

                await tx.CommitAsync(ct);
                return Results.Ok(new { userId });
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
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

        app.MapGet("/api/v1/users/lookup", async (
            IUserRepository users,
            CancellationToken ct) =>
        {
            var items = await users.ListAsync(1, 500, ct);
            return Results.Ok(new
            {
                items = items.Select(u => new
                {
                    userId = u.UserId,
                    login = u.Login,
                }).ToList()
            });
        })
        .RequireAuthorization()
        .WithName("LookupUsers")
        .WithSummary("Краткий список логинов для выбора в чатах");

        return app; // <-- ВОТ ЭТА СТРОКА БЫЛА УДАЛЕНА, ВЕРНУЛИ ОБРАТНО
    }

    public record CreateUserRequest(
        string Login,
        string Password,
        string? Email,
        int RoleId,
        string? FirstName,
        string? LastName,
        string? Patronymic,
        DateTime? BirthDate);
    public record UpdateUserRequest(string? Email, int RoleId, bool IsActive, string? NewPassword);
}