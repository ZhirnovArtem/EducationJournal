using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendance(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/attendance")
                   .WithTags("Attendance")
                   .RequireAuthorization();

        g.MapGet("/{monthScheduleId:int}", async Task<IResult> (
            int monthScheduleId,
            IAttendanceRepository attendance,
            CancellationToken ct) =>
        {
            var sheet = await attendance.GetSheetAsync(monthScheduleId, ct)
                ?? throw new NotFoundException("Занятие не найдено");
            return Results.Ok(sheet);
        })
        .RequireAuthorization("TeacherOrAdmin");

        g.MapPost("/{scheduleId:int}", async Task<IResult> (
            int scheduleId,
            SaveAttendanceRequest req,
            IAttendanceRepository attendance,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (req.Marks is null || req.Marks.Count == 0)
                throw new BusinessException("Нет отметок для сохранения");

            var marks = req.Marks
                .Select(m => (m.StudentId, m.AttendanceId, m.Comment))
                .ToList();

            await attendance.SaveAsync(scheduleId, current.UserId, marks, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin");

        return app;
    }

    public record MarkDto(int StudentId, int AttendanceId, string? Comment);
    public record SaveAttendanceRequest(List<MarkDto>? Marks);
}