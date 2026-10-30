using Microsoft.AspNetCore.Routing;
using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;

namespace EducationServer.Api.Endpoints;

public static class GradeSheetEndpoints
{
    public static IEndpointRouteBuilder MapGradeSheet(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/schedule")
                   .WithTags("GradeSheet")
                   .RequireAuthorization();

        // GET /api/v1/schedule/{monthScheduleId}/grades
        g.MapGet("/{monthScheduleId:int}/grades", async Task<IResult> (
            int monthScheduleId,
            IGradeSheetRepository repo,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            var sheet = await repo.GetSheetAsync(monthScheduleId, ct);
            if (sheet is null) return Results.NotFound();

            return Results.Ok(new
            {
                scheduleId = sheet.ScheduleId,
                monthScheduleId = sheet.MonthScheduleId,
                date = sheet.Date.ToString("yyyy-MM-dd"),
                disciplineTitle = sheet.DisciplineTitle,
                groupTitle = sheet.GroupTitle,
                pairNumber = sheet.PairNumber,
                startTime = sheet.StartTime.ToString("HH:mm"),
                endTime = sheet.EndTime.ToString("HH:mm"),
                students = sheet.Students.Select(s => new
                {
                    studentId = s.StudentId,
                    studentName = $"{s.LastName} {s.FirstName}{(s.Patronymic is null ? "" : " " + s.Patronymic)}",
                    gradeId = s.GradeId,
                    gradeValue = s.GradeValue,
                    comment = s.Comment,
                }),
                grades = sheet.Grades.Select(x => new
                {
                    gradeId = x.GradeId,
                    gradeValue = x.GradeValue,
                }),
            });
        })
        .WithName("GetGradeSheet")
        .WithSummary("Список студентов и их оценок на паре");

        // POST /api/v1/schedule/{monthScheduleId}/grades
        g.MapPost("/{monthScheduleId:int}/grades", async Task<IResult> (
            int monthScheduleId,
            SaveGradesRequest req,
            IGradeSheetRepository repo,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            var sheet = await repo.GetSheetAsync(monthScheduleId, ct)
                ?? throw new NotFoundException("Lesson not found");

            var marks = req.Marks
                .Select(m => (m.StudentId, m.GradeId, m.Comment))
                .ToList();

            await repo.SaveAsync(sheet.ScheduleId, current.UserId, marks, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("SaveGrades")
        .WithSummary("Сохранить оценки студентов за пару");

        return app;
    }

    public record SaveGradesRequest(IReadOnlyList<GradeMark> Marks);
    public record GradeMark(int StudentId, int GradeId, string? Comment);
}