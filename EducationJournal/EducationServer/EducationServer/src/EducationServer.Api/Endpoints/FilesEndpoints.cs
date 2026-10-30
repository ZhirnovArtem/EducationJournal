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

        app.MapGet("/files/{bucket}/{**objectKey}", async Task<IResult> (
            string bucket,
            string objectKey,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            objectKey = Uri.UnescapeDataString(objectKey);
            var rootCfg = cfg["Storage:LocalRoot"] ?? "uploads";
            var root = Path.IsPathRooted(rootCfg)
                ? rootCfg
                : Path.Combine(AppContext.BaseDirectory, rootCfg);

            var relative = $"{bucket}/{objectKey}".Replace('\\', '/');
            var full = Path.GetFullPath(Path.Combine(root, relative));

            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();

            if (!System.IO.File.Exists(full))
                return Results.NotFound(new
                {
                    triedPath = full,
                    root = root,
                    rootCfg = rootCfg,
                    directoryExists = Directory.Exists(root),
                    bucketExists = Directory.Exists(Path.Combine(root, bucket)),
                });

            var contentType = objectKey.ToLowerInvariant() switch
            {
                var k when k.EndsWith(".pdf") => "application/pdf",
                var k when k.EndsWith(".docx") => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                var k when k.EndsWith(".doc") => "application/msword",
                var k when k.EndsWith(".xlsx") => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                var k when k.EndsWith(".zip") => "application/zip",
                var k when k.EndsWith(".png") => "image/png",
                var k when k.EndsWith(".jpg") || k.EndsWith(".jpeg") => "image/jpeg",
                _ => "application/octet-stream",
            };

            return Results.File(full, contentType, enableRangeProcessing: true);
        })
        .WithName("GetRawFile")
        .WithSummary("Скачивание локального файла по bucket/key")
        .AllowAnonymous();
        return app;
    }
}