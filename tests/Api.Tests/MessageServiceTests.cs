using Api.Models.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class MessageServiceTests
{
    [Fact]
    public async Task AddUserMessage_ExistingConversation_PersistsMessageWithUserRole()
    {
        using var db = new TestDb();
        var student = new Student { Name = "Ana", TelegramChatId = 1, Conversation = new Conversation() };
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        var service = new MessageService(db.Context);

        var result = await service.AddUserMessageAsync(
            student.Conversation!.Id, "  Quanto custa?  ", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(MessageRole.User, result.Role);
        Assert.Equal("Quanto custa?", result.Content);
        var saved = await db.Context.Messages.SingleAsync();
        Assert.Equal(student.Conversation.Id, saved.ConversationId);
    }

    [Fact]
    public async Task AddUserMessage_UnknownConversation_ReturnsNullAndSavesNothing()
    {
        using var db = new TestDb();
        var service = new MessageService(db.Context);

        var result = await service.AddUserMessageAsync(999, "oi", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await db.Context.Messages.CountAsync());
    }
}
