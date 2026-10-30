namespace EducationServer.Data.Abstractions;

public sealed record GradeHistoryItem(
    string Date,        
    string Discipline,
    int GradeValue,
    string WorkType,   
    string? Comment);

public sealed record DisciplineAverage(
    string Discipline,
    decimal Average,
    int Count);

public sealed record GradeHistorySummary(
    decimal? AverageGrade,
    IReadOnlyList<DisciplineAverage> ByDiscipline);

public interface IGradeHistoryRepository
{
    Task<IReadOnlyList<GradeHistoryItem>> ListForStudentAsync(
        int studentId, CancellationToken ct);

    Task<GradeHistorySummary> GetSummaryAsync(
        int studentId, CancellationToken ct);
}