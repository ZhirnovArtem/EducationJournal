using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using EducationServer.Services.Abstractions;
using Microsoft.Extensions.Configuration;

namespace EducationServer.Services;

public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly int _presignedMinutes;

    public S3FileStorage(IConfiguration cfg)
    {
        var endpoint = cfg["Storage:Endpoint"]!;
        var accessKey = cfg["Storage:AccessKey"]!;
        var secretKey = cfg["Storage:SecretKey"]!;

        var s3cfg = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
        };
        _s3 = new AmazonS3Client(accessKey, secretKey, s3cfg);
        _presignedMinutes = int.TryParse(cfg["Storage:PresignedUrlMinutes"], out var m) ? m : 15;
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content, string fileName, string contentType,
        string bucket, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName);
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        ms.Position = 0;
        await _s3.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);

        return new StoredFileResult(bucket, key, bytes.LongLength, sha);
    }

    public async Task<Stream> OpenReadAsync(string bucket, string objectKey, CancellationToken ct)
    {
        var resp = await _s3.GetObjectAsync(bucket, objectKey, ct);
        return resp.ResponseStream;
    }

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken ct)
        => _s3.DeleteObjectAsync(bucket, objectKey, ct);

    public string GetPublicUrl(string bucket, string objectKey)
        => _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_presignedMinutes),
        });
}