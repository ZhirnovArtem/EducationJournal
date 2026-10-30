using EducationServer.Api.Auth;
using EducationServer.Api.Endpoints;
using EducationServer.Api.Hubs;
using EducationServer.Api.Middleware;
using EducationServer.Data;
using EducationServer.Data.Abstractions;
using EducationServer.Data.Repositories;
using EducationServer.Services;
using EducationServer.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IHomeworkRepository, HomeworkRepository>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IArchiveRepository, ArchiveRepository>();

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtProvider, JwtProvider>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HomeworkService>();
builder.Services.AddScoped<ArchiveService>();
builder.Services.AddScoped<IDisciplineRepository, DisciplineRepository>();

var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ITeacherRepository, TeacherRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IGradeSheetRepository, GradeSheetRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<IGradeHistoryRepository, GradeHistoryRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();

builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddSignalR(); // <--- Оставляем только один вызов SignalR

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddOpenApi();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 200L * 1024 * 1024;
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseDefaultFiles();
app.UseStaticFiles();

// ── МАРШРУТИЗАЦИЯ ДЛЯ LIVECODES ──
// Позволяет открывать /livecodes как обычную папку (отдаёт index.html)
app.MapFallbackToFile("/livecodes/{*path:nonfile}", "livecodes/index.html");
app.MapFallbackToFile("/livecodes", "livecodes/index.html");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "EducationServer API v1");
        c.RoutePrefix = "swagger";
    });
}

// app.UseHttpsRedirection();  <--- ЗАКОММЕНТИРОВАНО (ломает работу за Cloudflare Tunnel)
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapGroups();
app.MapStudents();
app.MapTeachers();
app.MapSchedule();
app.MapHomeworks();
app.MapUsers();
app.MapDisciplines();
app.MapAttendance();
app.MapArchives();
app.MapDashboard();
app.MapFiles();
app.MapGradeSheet();
app.MapGrades();
app.MapChats();
app.MapHealth();
app.MapIde();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();