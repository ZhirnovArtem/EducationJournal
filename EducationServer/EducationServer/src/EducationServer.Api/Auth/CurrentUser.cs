using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EducationServer.Api.Auth;
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor)
        => _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    public int UserId
    {
        get
        {
            var value = FindClaim(ClaimTypes.NameIdentifier)
                     ?? FindClaim("sub");
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
    public string Login
        => FindClaim("login") ?? string.Empty;
    public string Email
        => FindClaim(ClaimTypes.Email)
        ?? FindClaim("email")
        ?? string.Empty;
    public string Role
        => FindClaim(ClaimTypes.Role)
        ?? FindClaim("role")
        ?? string.Empty;

    public bool IsAuthenticated
        => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin
        => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsTeacher
        => string.Equals(Role, "Teacher", StringComparison.OrdinalIgnoreCase);

    public bool IsStudent
        => string.Equals(Role, "Student", StringComparison.OrdinalIgnoreCase);
    private string? FindClaim(string type)
        => _accessor.HttpContext?.User?.FindFirst(type)?.Value;
}