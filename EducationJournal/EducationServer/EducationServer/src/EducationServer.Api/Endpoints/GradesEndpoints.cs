using EducationServer.Data.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class GradesEndpoints
{
	public static IEndpointRouteBuilder MapGrades(this IEndpointRouteBuilder app)
	{
		var g = app.MapGroup("/api/v1/students")
				   .WithTags("Grades")
				   .RequireAuthorization();

		// GET /api/v1/students/{id}/grades
		g.MapGet("/{studentId:int}/grades", async Task<IResult> (
			int studentId,
			IGradeHistoryRepository repo,
			CancellationToken ct) =>
		{
			var items = await repo.ListForStudentAsync(studentId, ct);
			return Results.Ok(new { items });
		})
		.WithName("GetStudentGrades")
		.WithSummary("История оценок студента");

		// GET /api/v1/students/{id}/grades/summary
		g.MapGet("/{studentId:int}/grades/summary", async Task<IResult> (
			int studentId,
			IGradeHistoryRepository repo,
			CancellationToken ct) =>
		{
			var summary = await repo.GetSummaryAsync(studentId, ct);
			return Results.Ok(summary);
		})
		.WithName("GetStudentGradesSummary")
		.WithSummary("Сводка оценок студента");

		return app;
	}
}