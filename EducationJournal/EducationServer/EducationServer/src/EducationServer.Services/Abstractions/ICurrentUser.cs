namespace EducationServer.Services.Abstractions;
public interface ICurrentUser
{
    int UserId { get; }
    string Role { get; }
    string Login { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsTeacher { get; }
    bool IsStudent { get; }
}