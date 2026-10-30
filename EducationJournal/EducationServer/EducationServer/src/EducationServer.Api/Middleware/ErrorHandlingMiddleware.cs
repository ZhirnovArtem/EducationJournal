using EducationServer.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net;
using System.Text.Json;

namespace EducationServer.Api.Middleware;
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    public ErrorHandlingMiddleware(
        RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(ctx, HttpStatusCode.NotFound, "Not Found", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(ctx, HttpStatusCode.Forbidden, "Forbidden", ex.Message);
        }
        catch (BusinessException ex)
        {
            await WriteAsync(ctx, HttpStatusCode.BadRequest, "Bad Request", ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(ctx, HttpStatusCode.Conflict, "Conflict", ex.Message);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            var message = ex.ConstraintName switch
            {
                var n when n?.Contains("login", StringComparison.OrdinalIgnoreCase) == true
                    => "Логин уже занят",
                var n when n?.Contains("email", StringComparison.OrdinalIgnoreCase) == true
                    => "Email уже занят",
                var n when n?.Contains("title", StringComparison.OrdinalIgnoreCase) == true
                    => "Такое название уже существует",
                var n when n?.Contains("phone", StringComparison.OrdinalIgnoreCase) == true
                    => "Такой номер телефона уже привязан",
                _ => "Значение уже используется"
            };

            _logger.LogWarning("Duplicate key at {Path}: {Constraint}", ctx.Request.Path, ex.ConstraintName);
            await WriteAsync(ctx, HttpStatusCode.Conflict, "Conflict", message);
        }
        catch (Npgsql.PostgresException ex)
        {
            _logger.LogError(ex, "Postgres error at {Path}", ctx.Request.Path);
            await WriteAsync(ctx, HttpStatusCode.BadRequest, "Database error", ex.MessageText);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception at {Path}", ctx.Request.Path);
            await WriteAsync(ctx, HttpStatusCode.InternalServerError,
                "Internal Server Error", "Unexpected error");
        }
    }
    private static async Task WriteAsync(
        HttpContext ctx, HttpStatusCode status, string title, string detail)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = $"https://api.dnevnik.local/errors/{(int)status}",
            title,
            status = (int)status,
            detail,
            traceId = ctx.TraceIdentifier,
        };
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(problem,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}