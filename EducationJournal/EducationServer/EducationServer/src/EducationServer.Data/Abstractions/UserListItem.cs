namespace EducationServer.Data.Abstractions;

public record UserListItem(
    int UserId,
    string Login,
    string Email,
    int RoleId,
    string RoleCode,
    bool IsActive,
    DateTime? LastLoginAt);