using EducationServer.Api.Auth;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EducationServer.Api.Endpoints;

public static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchives(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/homeworks")
                   .WithTags("Homeworks.Archive")
                   .RequireAuthorization();

        // ─────────────────────────────────────────────────────────────
        // GET /api/v1/homeworks/{id}/archive
        // Presigned URL на весь ZIP-архив ответа студента
        // ─────────────────────────────────────────────────────────────
        g.MapGet("/{id:int}/archive", async Task<IResult> (
            int id,
            HomeworkService svc,
            IArchiveRepository archives,
            IFileRepository files,
            IFileStorage storage,
            CurrentUser current,
            CancellationToken ct) =>
        {
            // Проверка прав: студент — только своё, преподаватель — только своё
            await svc.GetAsync(id, current.UserId, current.Role, ct);

            var archiveId = await svc.GetArchiveIdAsync(id, ct)
                ?? throw new NotFoundException("Archive not found");

            var archive = await archives.FindByIdAsync(archiveId, ct)
                ?? throw new NotFoundException("Archive not found");

            var file = await files.FindByIdAsync(archive.FileId, ct)
                ?? throw new NotFoundException("File not found");

            var url = storage.GetPublicUrl(file.Bucket, file.ObjectKey);

            return Results.Ok(new
            {
                url,
                fileName = archive.OriginalName,
                sizeBytes = archive.CompressedBytes,
                fileCount = archive.FileCount,
                compressionRatio = archive.CompressionRatio,
                expiresAt = DateTime.UtcNow.AddMinutes(15),
            });
        })
        .WithName("GetHomeworkArchive")
        .WithSummary("Presigned URL на ZIP-архив ответа студента")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // ─────────────────────────────────────────────────────────────
        // GET /api/v1/homeworks/{id}/archive/entries
        // Список файлов внутри ZIP-архива
        // ─────────────────────────────────────────────────────────────
        g.MapGet("/{id:int}/archive/entries", async Task<IResult> (
            int id,
            HomeworkService svc,
            ArchiveService archiveSvc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            await svc.GetAsync(id, current.UserId, current.Role, ct);

            var archiveId = await svc.GetArchiveIdAsync(id, ct)
                ?? throw new NotFoundException("Archive not found");

            var entries = await archiveSvc.ListEntriesAsync(archiveId, ct);

            return Results.Ok(entries.Select(e => new
            {
                path = e.PathInArchive,
                mimeType = e.MimeType,
                sizeBytes = e.SizeBytes,
                compressedSize = e.CompressedSize,
            }));
        })
        .WithName("ListArchiveEntries")
        .WithSummary("Список файлов внутри ZIP-архива")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ─────────────────────────────────────────────────────────────
        // GET /api/v1/homeworks/{id}/archive/entries/{*path}
        // Скачать один файл из архива
        // ─────────────────────────────────────────────────────────────
        g.MapGet("/{id:int}/archive/entries/{*path}", async Task<IResult> (
            int id,
            string path,
            HomeworkService svc,
            ArchiveService archiveSvc,
            CurrentUser current,
            CancellationToken ct) =>
        {
            await svc.GetAsync(id, current.UserId, current.Role, ct);

            var archiveId = await svc.GetArchiveIdAsync(id, ct)
                ?? throw new NotFoundException("Archive not found");

            var bytes = await archiveSvc.ExtractFileAsync(archiveId, path, ct);
            var mime = GuessMime(path);

            return Results.File(bytes, mime, Path.GetFileName(path));
        })
        .WithName("GetArchiveEntry")
        .WithSummary("Скачать один файл из ZIP-архива")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // ─────────────────────────────────────────────────────────────
    // Определение MIME-типа по расширению
    // ─────────────────────────────────────────────────────────────
    private static string GuessMime(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" or ".cs" or ".js" or ".ts" or ".py" or ".md" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream",
        };
    }
}