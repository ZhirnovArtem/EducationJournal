using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EducationServer.Api.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuth(
        this IServiceCollection services, IConfiguration cfg)
    {
        var secret = cfg["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is missing");
        if (secret.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 chars");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = cfg["Jwt:Issuer"],
                    ValidAudience = cfg["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Cookies.TryGetValue("access_token", out var cookie))
                            ctx.Token = cookie;
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", p => p.RequireRole("Admin"));
            options.AddPolicy("Teacher", p => p.RequireRole("Teacher", "Admin"));
            options.AddPolicy("Student", p => p.RequireRole("Student"));
            options.AddPolicy("TeacherOrAdmin", p => p.RequireRole("Teacher", "Admin"));
        });

        return services;
    }
}