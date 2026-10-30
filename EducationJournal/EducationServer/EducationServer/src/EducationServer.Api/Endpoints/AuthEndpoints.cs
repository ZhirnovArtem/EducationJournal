using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Services.Abstractions;
using EducationServer.Domain;
using EducationServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth")
                   .WithTags("Auth");

        // ──────────────────────────────────────────────
        // POST /api/v1/auth/login
        // ──────────────────────────────────────────────
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
        .WithSummary("Авторизация по логину/паролю");

        // ──────────────────────────────────────────────
        // POST /api/v1/auth/refresh
        // ──────────────────────────────────────────────
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
        .WithSummary("Обновление access-токена по refresh-токену");

        // ──────────────────────────────────────────────
        // POST /api/v1/auth/logout
        // ──────────────────────────────────────────────
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
        .WithSummary("Выход: отзыв refresh-токена и удаление cookies");

        // ──────────────────────────────────────────────
        // GET /api/v1/auth/me
        // ──────────────────────────────────────────────
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
        .WithSummary("Текущий пользователь");

        // ──────────────────────────────────────────────
        // POST /api/v1/auth/dev-register
        // ──────────────────────────────────────────────
        g.MapPost("/dev-register", async Task<IResult> (
            string login, string password, string role,
            IPasswordHasher hasher,
            IUserRepository users,
            CancellationToken ct) =>
        {
            var u = new User
            {
                Login = login,
                Email = login + "@local",
                PasswordHash = hasher.Hash(password),
                PasswordAlgo = "argon2id",
                RoleId = role switch
                {
                    "Admin" => 1,
                    "Teacher" => 2,
                    _ => 3,
                },
            };
            var id = await users.InsertAsync(u, ct);
            return Results.Ok(new { userId = id });
        })
        .AllowAnonymous()
        .WithName("DevRegister")
        .WithSummary("DEV: создание пользователя без проверки прав");

        return app;
    }
}