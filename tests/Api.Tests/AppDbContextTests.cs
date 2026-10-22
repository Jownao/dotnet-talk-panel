using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class AppDbContextTests
{
    [Fact]
    public async Task Students_WithSameTelegramChatId_ViolatesUniqueIndex()
    {
        using var db = new TestDb();
        db.Context.Students.Add(new Student { Name = "Ana", TelegramChatId = 111 });
        await db.Context.SaveChangesAsync();

        db.Context.Students.Add(new Student { Name = "Outra Ana", TelegramChatId = 111 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Message_RoleAndRating_AreStoredAsStrings()
    {
        using var db = new TestDb();
        var student = new Student { Name = "Ana", TelegramChatId = 111, Conversation = new Conversation() };
        student.Conversation.Messages.Add(new Message
        {
            Role = MessageRole.Agent,
            Content = "Olá!",
            Rating = MessageRating.Failure
        });
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();

        var role = await db.Context.Database
            .SqlQueryRaw<string>("SELECT \"Role\" AS \"Value\" FROM \"Messages\"")
            .SingleAsync();

        Assert.Equal("Agent", role);
    }
}
