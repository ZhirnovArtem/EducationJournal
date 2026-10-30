namespace EducationServer.Data.Abstractions;

public record ChatListItem(
    int ChatId,
    string Title,
    int MessageCount,
    DateTime LastMessageAt);

public record ChatMessageItem(
    int ChatMessageId,
    int UserId,
    string UserName,
    string Text,
    DateTime CreatedAt);

public record ChatMemberItem(
    int UserId,
    string Login);

public interface IChatRepository
{
    Task<IReadOnlyList<ChatListItem>> ListChatsAsync(int userId, CancellationToken ct);
    Task<int> CreateChatAsync(string title, int createdByUserId, IReadOnlyList<int> memberIds, CancellationToken ct);
    Task<IReadOnlyList<ChatMessageItem>> ListMessagesAsync(int chatId, CancellationToken ct);
    Task<int> AddMessageAsync(int chatId, int userId, string text, CancellationToken ct);
    Task AddMemberAsync(int chatId, int userId, CancellationToken ct);
    Task<IReadOnlyList<int>> ListMemberIdsAsync(int chatId, CancellationToken ct);
    Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct);
}