using System.Security.Cryptography;
using EducationServer.Services.Abstractions;
using Microsoft.Extensions.Configuration;

namespace EducationServer.Services;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    public LocalFileStorage(IConfiguration cfg)
    {
        _root = cfg["Storage:LocalRoot"] ?? "./uploads";
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content, string fileName, string contentType,
        string bucket, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName);
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(_root, bucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = File.Create(full))
        {
            await content.CopyToAsync(fs, ct);
        }

        var info = new FileInfo(full);
        await using var read = File.OpenRead(full);
        var hash = await SHA256.HashDataAsync(read, ct);
        var sha = Convert.ToHexString(hash).ToLowerInvariant();

        return new StoredFileResult(bucket, key, info.Length, sha);
    }

    public Task<Stream> OpenReadAsync(string bucket, string objectKey, CancellationToken ct)
        => Task.FromResult<Stream>(File.OpenRead(Path.Combine(_root, bucket, objectKey)));

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken ct)
    {
        var full = Path.Combine(_root, bucket, objectKey);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string bucket, string objectKey)
        => $"/files/{bucket}/{objectKey}";
}