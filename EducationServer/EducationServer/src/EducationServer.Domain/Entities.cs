namespace EducationServer.Domain;

public class User
{
    public int UserId { get; set; }
    public string Login { get; set; } = default!;
    public string Email { get; set; } = default!;
    public byte[] PasswordHash { get; set; } = default!;
    public string PasswordAlgo { get; set; } = "argon2id";
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class Role
{
    public int RoleId { get; set; }
    public string Code { get; set; } = default!;
    public string Title { get; set; } = default!;
}

public class Student
{
    public int StudentId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Patronymic { get; set; }
    public DateTime BirthDate { get; set; }
    public int? GroupId { get; set; }
    public string? GroupTitle { get; set; }
    public string Email { get; set; } = default!;
    public long? PhotoFileId { get; set; }
}

public class Teacher
{
    public int TeacherId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Patronymic { get; set; }
    public string Email { get; set; } = default!;
}

public class Homework
{
    public int HomeworkId { get; set; }
    public int StudentId { get; set; }
    public int TeacherId { get; set; }
    public int DisciplineId { get; set; }
    public string Task { get; set; } = default!;
    public long? TaskFileId { get; set; }
    public string? HomeworkText { get; set; }
    public long? AnswerFileId { get; set; }
    public long? AnswerArchiveId { get; set; }
    public DateOnly DateOfIssue { get; set; }
    public DateOnly DeadLine { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? GradeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public record HomeworkListItem(
    int HomeworkId,
    string Task,
    string Discipline,
    string TeacherName,
    DateOnly DateOfIssue,
    DateOnly DeadLine,
    DateOnly? DueDate,
    int? Grade,
    long? TaskFileId,
    long? AnswerArchiveId);

public class HomeworkArchive
{
    public long ArchiveId { get; set; }
    public long FileId { get; set; }
    public string OriginalName { get; set; } = default!;
    public int FileCount { get; set; }
    public long UncompressedBytes { get; set; }
    public long CompressedBytes { get; set; }
    public decimal? CompressionRatio { get; set; }
    public string Algorithm { get; set; } = "zip-deflate";
    public bool IsEncrypted { get; set; }
    public byte[]? PasswordHash { get; set; }
    public string Checksum { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class HomeworkArchiveFile
{
    public long ArchiveFileId { get; set; }
    public long ArchiveId { get; set; }
    public string PathInArchive { get; set; } = default!;
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public long? CompressedSize { get; set; }
    public string? Checksum { get; set; }
}

public class FileEntity
{
    public long FileId { get; set; }
    public string Bucket { get; set; } = default!;
    public string ObjectKey { get; set; } = default!;
    public string OriginalName { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = default!;
    public int UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class RefreshTokenRow
{
    public long RefreshTokenId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public record LoginAttempt(string Login, bool Success, string? Ip, string? UserAgent);