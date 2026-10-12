using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class FilesEndpoints
{
    public static IEndpointRouteBuilder MapFiles(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/files")
                   .WithTags("Files")
                   .RequireAuthorization();
        g.MapGet("/{id:long}", async Task<IResult> (
            long id,
            IFileRepository files,
            CancellationToken ct) =>
        {
            var file = await files.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("File not found");

            return Results.Ok(new
            {
                fileId = file.FileId,
                originalName = file.OriginalName,
                mimeType = file.MimeType,
                sizeBytes = file.SizeBytes,
                uploadedAt = file.UploadedAt,
            });
        })
        .WithName("GetFile")
        .WithSummary("Метаданные файла")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
        g.MapGet("/{id:long}/url", async Task<IResult> (
            long id,
            IFileRepository files,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var file = await files.FindByIdAsync(id, ct)
                ?? throw new NotFoundException("File not found");

            var url = storage.GetPublicUrl(file.Bucket, file.ObjectKey);

            return Results.Ok(new
            {
                url,
                expiresAt = DateTime.UtcNow.AddMinutes(15),
            });
        })
        .WithName("GetFileUrl")
        .WithSummary("Presigned URL для скачивания файла")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
        g.MapDelete("/{id:long}", async Task<IResult> (
            long id,
            IFileRepository files,
            CurrentUser current,
            CancellationToken ct) =>
        {
            if (!current.IsTeacher && !current.IsAdmin)
                throw new ForbiddenException();

            await files.SoftDeleteAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("TeacherOrAdmin")
        .WithName("DeleteFile")
        .WithSummary("Мягкое удаление файла")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}