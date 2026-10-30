using System.IO.Compression;
using System.Security.Cryptography;
using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EducationServer.Services;

public sealed class ArchiveService
{
    public const string Bucket = "edu-homework-archives";
    private const long MaxUncompressedBytes = 200L * 1024 * 1024;
    private const int MaxFileCount = 100;

    private readonly IFileRepository _files;
    private readonly IArchiveRepository _archives;
    private readonly IFileStorage _storage;
    private readonly ILogger<ArchiveService> _log;

    public ArchiveService(
        IFileRepository files,
        IArchiveRepository archives,
        IFileStorage storage,
        ILogger<ArchiveService> log)
    {
        _files = files;
        _archives = archives;
        _storage = storage;
        _log = log;
    }

    public async Task<HomeworkArchive> CreateFromFilesAsync(
        int homeworkId, IReadOnlyList<IFormFile> files,
        int uploadedBy, CancellationToken ct)
    {
        if (files.Count == 0)
            throw new BusinessException("No files to archive");
        if (files.Count > MaxFileCount)
            throw new BusinessException($"Too many files (max {MaxFileCount})");

        var tmpZip = Path.GetTempFileName();
        long uncompressed = 0;
        var entries = new List<HomeworkArchiveFile>();

        try
        {
            await using (var fs = File.Create(tmpZip))
            await using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var f in files)
                {
                    if (f.Length > 50L * 1024 * 1024)
                        throw new BusinessException($"File '{f.FileName}' too large (max 50 MB)");

                    var entryName = SanitizeEntryName(f.FileName);
                    var entry = zip.CreateEntry(entryName, ChooseLevel(entryName));

                    await using var src = f.OpenReadStream();
                    await using var dst = entry.Open();
                    await src.CopyToAsync(dst, ct);

                    uncompressed += f.Length;
                    if (uncompressed > MaxUncompressedBytes)
                        throw new BusinessException("Archive too large");

                    entries.Add(new HomeworkArchiveFile
                    {
                        PathInArchive = entryName,
                        MimeType = f.ContentType,
                        SizeBytes = f.Length,
                    });
                }
            }

            var compressed = new FileInfo(tmpZip).Length;
            var checksum = await ComputeSha256Async(tmpZip, ct);

            await using (var zipStream = File.OpenRead(tmpZip))
            {
                var saved = await _storage.SaveAsync(
                    zipStream,
                    $"homework_{homeworkId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip",
                    "application/zip",
                    Bucket,
                    ct);

                var fileEntity = new FileEntity
                {
                    Bucket = saved.Bucket,
                    ObjectKey = saved.ObjectKey,
                    OriginalName = $"homework_{homeworkId}.zip",
                    MimeType = "application/zip",
                    SizeBytes = saved.Size,
                    Checksum = saved.Sha256,
                    UploadedBy = uploadedBy,
                };
                var fileId = await _files.InsertAsync(fileEntity, ct);

                var archive = new HomeworkArchive
                {
                    FileId = fileId,
                    OriginalName = fileEntity.OriginalName,
                    FileCount = entries.Count,
                    UncompressedBytes = uncompressed,
                    CompressedBytes = compressed,
                    CompressionRatio = uncompressed > 0
                        ? Math.Round(100m * compressed / uncompressed, 2)
                        : null,
                    Algorithm = "zip-deflate",
                    Checksum = checksum,
                };
                var archiveId = await _archives.InsertArchiveAsync(archive, ct);

                foreach (var e in entries) e.ArchiveId = archiveId;
                await _archives.InsertEntriesAsync(archiveId, entries, ct);

                archive.ArchiveId = archiveId;
                return archive;
            }
        }
        finally
        {
            try { File.Delete(tmpZip); } catch { /* ignore */ }
        }
    }

    public async Task<byte[]> ExtractFileAsync(
        long archiveId, string pathInArchive, CancellationToken ct)
    {
        var archive = await _archives.FindByIdAsync(archiveId, ct)
            ?? throw new NotFoundException("Archive not found");
        var file = await _files.FindByIdAsync(archive.FileId, ct)
            ?? throw new NotFoundException("Archive file not found");

        await using var zipStream = await _storage.OpenReadAsync(
            file.Bucket, file.ObjectKey, ct);

        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry(pathInArchive)
            ?? throw new NotFoundException($"Entry '{pathInArchive}' not found");

        await using var entryStream = entry.Open();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public Task<IReadOnlyList<HomeworkArchiveFile>> ListEntriesAsync(
        long archiveId, CancellationToken ct)
        => _archives.ListEntriesAsync(archiveId, ct);

    private static string SanitizeEntryName(string name)
    {
        var cleaned = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "file";
        if (cleaned.Contains("..") || Path.IsPathRooted(cleaned))
            throw new BusinessException("Invalid file name");
        return cleaned;
    }

    private static CompressionLevel ChooseLevel(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"
                or ".mp4" or ".mov" or ".avi" or ".mkv"
                or ".zip" or ".7z" or ".rar" or ".gz"
                or ".docx" or ".xlsx" or ".pptx" or ".pdf"
                => CompressionLevel.NoCompression,

            ".txt" or ".md" or ".csv" or ".json" or ".xml"
                or ".cs" or ".py" or ".js" or ".ts"
                or ".html" or ".css"
                => CompressionLevel.SmallestSize,

            _ => CompressionLevel.Optimal,
        };
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}