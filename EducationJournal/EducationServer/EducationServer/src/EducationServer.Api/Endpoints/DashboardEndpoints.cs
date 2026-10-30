using Microsoft.AspNetCore.Routing;
using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services;

namespace EducationServer.Api.Endpoints;

public static class DashboardEndpoints
{
	public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
	{
		var g = app.MapGroup("/api/v1/dashboard")
				   .WithTags("Dashboard")
				   .RequireAuthorization();

		g.MapGet("/", async Task<IResult> (
			DashboardService svc,
			CurrentUser current,
			CancellationToken ct) =>
		{
			var dto = await svc.GetAsync(current.UserId, current.Role, ct);
			return Results.Ok(dto);
		})
		.WithName("GetDashboard")
		.WithSummary("Данные главной страницы");

		return app;
	}
}