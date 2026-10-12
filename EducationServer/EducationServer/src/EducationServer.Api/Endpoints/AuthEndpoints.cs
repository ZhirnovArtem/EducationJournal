using EducationServer.Api.Auth;
using EducationServer.Domain;
using EducationServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using EducationServer.Data.Abstractions;
using EducationServer.Services.Abstractions;

namespace EducationServer.Api.Endpoints;
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth")
                   .WithTags("Auth");
        g.MapPost("/login", async Task<IResult> (
            LoginRequest req,
            AuthService svc,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var result = await svc.LoginAsync(req, ip, ua, ct);
            http.Response.Cookies.Append("refresh_token", result.RefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                });
            http.Response.Cookies.Append("access_token", result.AccessToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(15),
                });

            return Results.Ok(new
            {
                accessToken = result.AccessToken,
                expiresAt = result.ExpiresAt,
            });
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Авторизация по логину/паролю")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        g.MapPost("/refresh", async Task<IResult> (
            HttpContext http,
            AuthService svc,
            CancellationToken ct) =>
        {
            var refresh = http.Request.Cookies["refresh_token"];
            if (string.IsNullOrWhiteSpace(refresh))
                throw new BusinessException("Refresh token missing");

            var ua = http.Request.Headers.UserAgent.ToString();
            var result = await svc.RefreshAsync(refresh, ua, ct);

            http.Response.Cookies.Append("refresh_token", result.RefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                });

            http.Response.Cookies.Append("access_token", result.AccessToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(15),
                });

            return Results.Ok(new
            {
                accessToken = result.AccessToken,
                expiresAt = result.ExpiresAt,
            });
        })
        .AllowAnonymous()
        .WithName("Refresh")
        .WithSummary("Обновление access-токена по refresh-токену")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
        g.MapPost("/logout", async Task<IResult> (
            HttpContext http,
            AuthService svc,
            CancellationToken ct) =>
        {
            var refresh = http.Request.Cookies["refresh_token"];
            if (!string.IsNullOrWhiteSpace(refresh))
                await svc.LogoutAsync(refresh, ct);

            http.Response.Cookies.Delete("refresh_token");
            http.Response.Cookies.Delete("access_token");

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Выход: отзыв refresh-токена и удаление cookies")
        .Produces(StatusCodes.Status204NoContent);
        g.MapGet("/me", async Task<IResult> (
            AuthService svc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsAuthenticated)
                throw new ForbiddenException("Not authenticated");

            var me = await svc.MeAsync(current.UserId, ct);
            return Results.Ok(me);
        })
        .RequireAuthorization()
        .WithName("Me")
        .WithSummary("Текущий пользователь")
        .Produces<MeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);
        g.MapPost("/dev-register", async Task<IResult> (
    string login, string password, string role,
    IPasswordHasher hasher,
    IUserRepository users, CancellationToken ct) =>
        {
            var u = new EducationServer.Domain.User
            {
                Login = login,
                Email = login + "@local",
                PasswordHash = hasher.Hash(password),
                PasswordAlgo = "argon2id",
                RoleId = role switch { "Admin" => 1, "Teacher" => 2, _ => 3 },
            };
            var id = await users.InsertAsync(u, ct);
            return Results.Ok(new { userId = id });
        }).AllowAnonymous();
        return app;
    }
}