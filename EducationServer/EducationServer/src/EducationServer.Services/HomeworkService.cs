using EducationServer.Data.Abstractions;
using EducationServer.Domain;
using Microsoft.AspNetCore.Http;

namespace EducationServer.Services;

public record HomeworkFilterDto(
    int? StudentId, int? DisciplineId,
    string? Status, DateOnly? From, DateOnly? To,
    int Page, int PageSize);

public record HomeworkListItemDto(
    int HomeworkId, string Task, string Discipline, string TeacherName,
    DateOnly DateOfIssue, DateOnly DeadLine, DateOnly? DueDate,
    int? Grade, string? TaskFileUrl, string? ArchiveUrl);

public record HomeworkDetailsDto(
    int HomeworkId, string Task, string? HomeworkText,
    string? TaskFileUrl, string? AnswerFileUrl, string? ArchiveUrl,
    DateOnly DateOfIssue, DateOnly DeadLine, DateOnly? DueDate,
    int? Grade);

public sealed class HomeworkService
{
    private readonly IHomeworkRepository _homeworks;
    private readonly IFileRepository _files;
    private readonly IArchiveRepository _archives;
    private readonly ArchiveService _archiveService;
    private readonly Abstractions.IFileStorage _storage;

    public HomeworkService(
        IHomeworkRepository homeworks,
        IFileRepository files,
        IArchiveRepository archives,
        ArchiveService archiveService,
        Abstractions.IFileStorage storage)
    {
        _homeworks = homeworks;
        _files = files;
        _archives = archives;
        _archiveService = archiveService;
        _storage = storage;
    }

    public async Task<(IReadOnlyList<HomeworkListItemDto> Items, int Total)> ListAsync(
        HomeworkFilterDto filter, int userId, string role, CancellationToken ct)
    {
        int? studentId = filter.StudentId;
        if (role == "Student")
            studentId = await _homeworks.GetStudentIdByUserAsync(userId, ct);

        var q = new HomeworkQuery(
            studentId, filter.DisciplineId,
            filter.Status, filter.From, filter.To,
            filter.Page, filter.PageSize);

        var (items, total) = await _homeworks.ListAsync(q, ct);

        var dtos = new List<HomeworkListItemDto>(items.Count);
        foreach (var item in items)
        {
            string? taskFileUrl = null;
            if (item.TaskFileId.HasValue)
            {
                var f = await _files.FindByIdAsync(item.TaskFileId.Value, ct);
                if (f is not null) taskFileUrl = _storage.GetPublicUrl(f.Bucket, f.ObjectKey);
            }

            string? archiveUrl = null;
            if (item.AnswerArchiveId.HasValue)
            {
                var a = await _archives.FindByIdAsync(item.AnswerArchiveId.Value, ct);
                if (a is not null)
                {
                    var f = await _files.FindByIdAsync(a.FileId, ct);
                    if (f is not null) archiveUrl = _storage.GetPublicUrl(f.Bucket, f.ObjectKey);
                }
            }

            dtos.Add(new HomeworkListItemDto(
                item.HomeworkId, item.Task, item.Discipline, item.TeacherName,
                item.DateOfIssue, item.DeadLine, item.DueDate,
                item.Grade, taskFileUrl, archiveUrl));
        }

        return (dtos, total);
    }

    public async Task<HomeworkDetailsDto> GetAsync(
        int homeworkId, int userId, string role, CancellationToken ct)
    {
        var hw = await _homeworks.FindByIdAsync(homeworkId, ct)
            ?? throw new NotFoundException("Homework not found");

        if (role == "Student")
        {
            var studentId = await _homeworks.GetStudentIdByUserAsync(userId, ct);
            if (hw.StudentId != studentId)
                throw new ForbiddenException();
        }

        string? taskFileUrl = null;
        if (hw.TaskFileId.HasValue)
        {
            var f = await _files.FindByIdAsync(hw.TaskFileId.Value, ct);
            if (f is not null) taskFileUrl = _storage.GetPublicUrl(f.Bucket, f.ObjectKey);
        }

        string? answerFileUrl = null;
        if (hw.AnswerFileId.HasValue)
        {
            var f = await _files.FindByIdAsync(hw.AnswerFileId.Value, ct);
            if (f is not null) answerFileUrl = _storage.GetPublicUrl(f.Bucket, f.ObjectKey);
        }

        string? archiveUrl = null;
        if (hw.AnswerArchiveId.HasValue)
        {
            var a = await _archives.FindByIdAsync(hw.AnswerArchiveId.Value, ct);
            if (a is not null)
            {
                var f = await _files.FindByIdAsync(a.FileId, ct);
                if (f is not null) archiveUrl = _storage.GetPublicUrl(f.Bucket, f.ObjectKey);
            }
        }

        return new HomeworkDetailsDto(
            hw.HomeworkId, hw.Task, hw.HomeworkText,
            taskFileUrl, answerFileUrl, archiveUrl,
            hw.DateOfIssue, hw.DeadLine, hw.DueDate,
            hw.GradeId);
    }

    public async Task SubmitAsync(
        int homeworkId, int userId, string? text,
        IReadOnlyList<IFormFile>? files, CancellationToken ct)
    {
        var studentId = await _homeworks.GetStudentIdByUserAsync(userId, ct);
        var hw = await _homeworks.FindByIdAsync(homeworkId, ct)
            ?? throw new NotFoundException("Homework not found");

        if (hw.StudentId != studentId)
            throw new ForbiddenException();

        if (hw.DeadLine < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessException("Deadline passed");

        long? archiveId = null;

        if (files is { Count: > 0 })
        {
            var archive = await _archiveService.CreateFromFilesAsync(
                homeworkId, files, userId, ct);
            archiveId = archive.ArchiveId;
        }

        await _homeworks.UpdateSubmissionAsync(homeworkId, text, null, archiveId, ct);
    }

    public async Task GradeAsync(int homeworkId, int? gradeId, CancellationToken ct)
    {
        var hw = await _homeworks.FindByIdAsync(homeworkId, ct)
            ?? throw new NotFoundException("Homework not found");

        await _homeworks.UpdateGradeAsync(homeworkId, gradeId, ct);
    }

    public async Task DeleteAsync(int homeworkId, CancellationToken ct)
        => await _homeworks.SoftDeleteAsync(homeworkId, ct);
    public Task<long?> GetArchiveIdAsync(int homeworkId, CancellationToken ct)
    => _homeworks.GetArchiveIdAsync(homeworkId, ct);
}