using Microsoft.AspNetCore.Http;
using EducationServer.Data.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace EducationServer.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
           .AllowAnonymous()
           .WithTags("Health");
        app.MapGet("/health/ready", async (
            IDbConnectionFactory factory, CancellationToken ct) =>
        {
            try
            {
                await using var conn = await factory.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync(ct);
                return Results.Ok(new { status = "ready" });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "not-ready",
                    error = ex.Message
                }, statusCode: 503);
            }
        })
        .AllowAnonymous()
        .WithTags("Health");

        return app;
    }
}