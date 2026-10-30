using Dapper;
using EducationServer.Data.Abstractions;

namespace EducationServer.Data.Repositories;

public sealed class ChatRepository : IChatRepository
{
    private readonly IDbConnectionFactory _factory;
    public ChatRepository(IDbConnectionFactory factory) => _factory = factory;

    // 1. Список чатов для конкретного пользователя
    public async Task<IReadOnlyList<ChatListItem>> ListChatsAsync(int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT c.ChatId, c.Title,
                   (SELECT COUNT(*) FROM ChatMessages m WHERE m.ChatId = c.ChatId AND m.DeletedAt IS NULL) AS MessageCount,
                   COALESCE((SELECT MAX(m.CreatedAt) FROM ChatMessages m WHERE m.ChatId = c.ChatId), c.CreatedAt) AS LastMessageAt
            FROM Chats c
            JOIN ChatMembers cm ON cm.ChatId = c.ChatId
            WHERE c.DeletedAt IS NULL
              AND cm.UserId = @uid
            ORDER BY LastMessageAt DESC";

        return await conn.QueryAsync(sql, r => new ChatListItem(
            r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetDateTime(3)),
            c => c.Bind("@uid", userId), ct);
    }

    // 2. Создание чата с участниками
    public async Task<int> CreateChatAsync(string title, int createdByUserId, IReadOnlyList<int> memberIds, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);

        // Создаем сам чат
        var chatId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO Chats (Title, CreatedBy) VALUES (@t, @u) RETURNING ChatId",
            c => c.Bind("@t", title).Bind("@u", createdByUserId), ct);

        // Создатель + все приглашённые
        var allMembers = (memberIds ?? Array.Empty<int>()).Append(createdByUserId).Distinct();

        foreach (var uid in allMembers)
        {
            await conn.ExecuteAsync(
                "INSERT INTO ChatMembers (ChatId, UserId) VALUES (@c, @u) ON CONFLICT DO NOTHING",
                c => c.Bind("@c", chatId).Bind("@u", uid), ct);
        }

        return chatId;
    }

    // 3. Сообщения чата
    public async Task<IReadOnlyList<ChatMessageItem>> ListMessagesAsync(int chatId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        const string sql = @"
            SELECT m.ChatMessageId, m.UserId, u.Login, m.Text, m.CreatedAt
            FROM ChatMessages m
            JOIN Users u ON u.UserId = m.UserId
            WHERE m.ChatId = @cid AND m.DeletedAt IS NULL
            ORDER BY m.CreatedAt ASC
            LIMIT 200";

        return await conn.QueryAsync(sql, r => new ChatMessageItem(
            r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetDateTime(4)),
            c => c.Bind("@cid", chatId), ct);
    }

    // 4. Отправка сообщения
    public async Task<int> AddMessageAsync(int chatId, int userId, string text, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO ChatMessages (ChatId, UserId, Text) VALUES (@c, @u, @t) RETURNING ChatMessageId",
            c => c.Bind("@c", chatId).Bind("@u", userId).Bind("@t", text), ct);
    }

    // 5. Добавление участника
    public async Task AddMemberAsync(int chatId, int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO ChatMembers (ChatId, UserId) VALUES (@c, @u) ON CONFLICT DO NOTHING",
            c => c.Bind("@c", chatId).Bind("@u", userId), ct);
    }

    // 6. Список ID участников чата
    public async Task<IReadOnlyList<int>> ListMemberIdsAsync(int chatId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return (await conn.QueryAsync<int>(
            "SELECT UserId FROM ChatMembers WHERE ChatId = @c",
            r => r.GetInt32(0),
            c => c.Bind("@c", chatId),
            ct)).ToList();
    }

    // 7. Проверка, является ли пользователь участником
    public async Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM ChatMembers WHERE ChatId = @c AND UserId = @u)",
            c => c.Bind("@c", chatId).Bind("@u", userId), ct);
    }
}