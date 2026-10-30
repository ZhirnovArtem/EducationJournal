namespace EducationServer.Services.Abstractions;

public record StoredFileResult(string Bucket, string ObjectKey, long Size, string Sha256);

public interface IFileStorage
{
    Task<StoredFileResult> SaveAsync(
        Stream content, string fileName, string contentType,
        string bucket, CancellationToken ct);

    Task<Stream> OpenReadAsync(string bucket, string objectKey, CancellationToken ct);

    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct);

    string GetPublicUrl(string bucket, string objectKey);
}