using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using EducationServer.Api.Auth;
using EducationServer.Api.Hubs;
using EducationServer.Data.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace EducationServer.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChats(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/chats").WithTags("Chats").RequireAuthorization();

        g.MapGet("/", async (IChatRepository repo, CurrentUser current, CancellationToken ct) =>
        {
            var chats = await repo.ListChatsAsync(current.UserId, ct);
            return Results.Ok(new { items = chats });
        });

        g.MapPost("/", async (CreateChatRequest req, IChatRepository repo, CurrentUser current, IHubContext<NotificationHub> hub, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.BadRequest(new { detail = "Название обязательно" });

            var memberIds = req.MemberIds ?? Array.Empty<int>();
            var id = await repo.CreateChatAsync(req.Title.Trim(), current.UserId, memberIds, ct);

            foreach (var uid in memberIds.Distinct())
            {
                if (uid == current.UserId) continue;
                try
                {
                    await hub.Clients.Group($"user-{uid}")
                        .SendAsync("ChatInvited", new { chatId = id }, ct);
                }
                catch { }
            }

            return Results.Ok(new { chatId = id });
        });

        g.MapGet("/{chatId:int}/messages", async (int chatId, IChatRepository repo, CurrentUser current, CancellationToken ct) =>
        {
            if (!await repo.IsMemberAsync(chatId, current.UserId, ct))
                return Results.Forbid();
            var msgs = await repo.ListMessagesAsync(chatId, ct);
            return Results.Ok(new { items = msgs });
        });

        g.MapPost("/{chatId:int}/messages", async (
            int chatId, SendMessageRequest req, IChatRepository repo,
            CurrentUser current, IHubContext<NotificationHub> hub, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new { detail = "Пустое сообщение" });

            if (!await repo.IsMemberAsync(chatId, current.UserId, ct))
                return Results.Forbid();

            var id = await repo.AddMessageAsync(chatId, current.UserId, req.Text.Trim(), ct);
            await hub.Clients.Group($"chat-{chatId}").SendAsync("MessageReceived", new
            {
                chatMessageId = id,
                chatId,
                userId = current.UserId,
                text = req.Text,
                createdAt = DateTime.UtcNow
            }, ct);
            return Results.Ok(new { messageId = id });
        });

        g.MapGet("/{chatId:int}/members", async (int chatId, IChatRepository repo, CurrentUser current, CancellationToken ct) =>
        {
            if (!await repo.IsMemberAsync(chatId, current.UserId, ct))
                return Results.Forbid();
            var ids = await repo.ListMemberIdsAsync(chatId, ct);
            return Results.Ok(new { userIds = ids });
        });

        g.MapPost("/{chatId:int}/members", async (
            int chatId, InviteRequest req, IChatRepository repo,
            CurrentUser current, IHubContext<NotificationHub> hub, CancellationToken ct) =>
        {
            if (req.UserId <= 0)
                return Results.BadRequest(new { detail = "Некорректный userId" });
            if (!await repo.IsMemberAsync(chatId, current.UserId, ct))
                return Results.Forbid();
            await repo.AddMemberAsync(chatId, req.UserId, ct);
            await hub.Clients.Group($"user-{req.UserId}")
                .SendAsync("ChatInvited", new { chatId }, ct);
            return Results.NoContent();
        });

        return app;
    }

    public record CreateChatRequest(string Title, IReadOnlyList<int>? MemberIds);
    public record SendMessageRequest(string Text);
    public record InviteRequest(int UserId);
}