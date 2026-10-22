using Api.Models.Dtos;
using Api.Models.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class ConversationServiceTests
{
    private static async Task<Conversation> AddConversationAsync(
        TestDb db, long chatId, string name, params Message[] messages)
    {
        var student = new Student { Name = name, TelegramChatId = chatId, Conversation = new Conversation() };
        student.Conversation.Messages.AddRange(messages);
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        return student.Conversation;
    }

    private static Message Msg(MessageRole role, string content, DateTime at, MessageRating? rating = null) =>
        new() { Role = role, Content = content, CreatedAt = at, Rating = rating };

    [Fact]
    public async Task List_OrdersByLastActivityAndShowsPreview()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        await AddConversationAsync(db, 1, "Antiga", Msg(MessageRole.User, "oi antigo", t));
        await AddConversationAsync(db, 2, "Recente",
            Msg(MessageRole.User, "oi", t.AddHours(1)),
            Msg(MessageRole.Agent, "Olá, tudo bem?", t.AddHours(2)));
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: false, CancellationToken.None);

        Assert.Equal(["Recente", "Antiga"], result.Select(r => r.StudentName));
        Assert.Equal("Olá, tudo bem?", result[0].LastMessagePreview);
        Assert.Equal(t.AddHours(2), result[0].LastActivityAt);
    }

    [Fact]
    public async Task List_HasFailuresTrue_ReturnsOnlyConversationsWithFailureRatedMessages()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        await AddConversationAsync(db, 1, "Com falha",
            Msg(MessageRole.Agent, "errei", t, MessageRating.Failure));
        await AddConversationAsync(db, 2, "Só boa",
            Msg(MessageRole.Agent, "acertei", t, MessageRating.Good));
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: true, CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal("Com falha", only.StudentName);
        Assert.Equal(1, only.FailureCount);
    }

    [Fact]
    public async Task List_EmptyConversation_UsesCreatedAtAndNullPreview()
    {
        using var db = new TestDb();
        var conversation = await AddConversationAsync(db, 1, "Sem mensagens");
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: false, CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Null(only.LastMessagePreview);
        Assert.Equal(conversation.CreatedAt, only.LastActivityAt);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsMessagesInChronologicalOrder()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        var conversation = await AddConversationAsync(db, 1, "Ana",
            Msg(MessageRole.Agent, "terceira", t.AddMinutes(3)),
            Msg(MessageRole.User, "primeira", t.AddMinutes(1)),
            Msg(MessageRole.Agent, "segunda", t.AddMinutes(2)));
        var service = new ConversationService(db.Context);

        var result = await service.GetByIdAsync(conversation.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ana", result.StudentName);
        Assert.Equal(["primeira", "segunda", "terceira"], result.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);

        var result = await service.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreate_NewChatId_CreatesStudentAndConversation()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);

        var (conversation, created) = await service.GetOrCreateAsync(
            new CreateConversationRequest(111, "Ana"), CancellationToken.None);

        Assert.True(created);
        Assert.Equal("Ana", conversation.StudentName);
        Assert.Equal(1, await db.Context.Students.CountAsync());
        Assert.Equal(1, await db.Context.Conversations.CountAsync());
    }

    [Fact]
    public async Task GetOrCreate_SameChatId_ReturnsExistingWithoutDuplicating()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);
        var request = new CreateConversationRequest(111, "Ana");

        var (first, _) = await service.GetOrCreateAsync(request, CancellationToken.None);
        var (second, created) = await service.GetOrCreateAsync(request, CancellationToken.None);

        Assert.False(created);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.Context.Students.CountAsync());
        Assert.Equal(1, await db.Context.Conversations.CountAsync());
    }
}
