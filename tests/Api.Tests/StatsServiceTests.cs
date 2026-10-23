using Api.Models.Entities;
using Api.Services;

namespace Api.Tests;

public class StatsServiceTests
{
    private static Message Agent(string version, MessageRating? rating = null) =>
        new() { Role = MessageRole.Agent, Content = "resposta", PromptVersion = version, Rating = rating };

    private static Message User() => new() { Role = MessageRole.User, Content = "pergunta" };

    private static async Task SeedAsync(TestDb db, long chatId, params Message[] messages)
    {
        var student = new Student { Name = $"Aluno {chatId}", TelegramChatId = chatId, Conversation = new Conversation() };
        student.Conversation.Messages.AddRange(messages);
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_EmptyDatabase_ReturnsZeros()
    {
        using var db = new TestDb();

        var stats = await new StatsService(db.Context).GetAsync(CancellationToken.None);

        Assert.Equal(0, stats.TotalConversations);
        Assert.Equal(0, stats.TotalAgentMessages);
        Assert.Equal(0, stats.ReviewedCount);
        Assert.Equal(0, stats.FailureRate);
        Assert.Empty(stats.ByPromptVersion);
    }

    [Fact]
    public async Task Get_CountsOnlyAgentMessagesAndComputesFailureRateOverReviewed()
    {
        using var db = new TestDb();
        await SeedAsync(db, 1,
            User(), Agent("v1", MessageRating.Good), Agent("v1", MessageRating.Failure), Agent("v1"));
        await SeedAsync(db, 2,
            User(), Agent("v1", MessageRating.Failure));
        // 2 conversas; 4 respostas do agente (3 avaliadas: 1 boa, 2 falhas)

        var stats = await new StatsService(db.Context).GetAsync(CancellationToken.None);

        Assert.Equal(2, stats.TotalConversations);
        Assert.Equal(4, stats.TotalAgentMessages);
        Assert.Equal(3, stats.ReviewedCount);
        Assert.Equal(1, stats.GoodCount);
        Assert.Equal(2, stats.FailureCount);
        Assert.Equal(2.0 / 3.0, stats.FailureRate, precision: 5);
    }

    [Fact]
    public async Task Get_GroupsByPromptVersion()
    {
        using var db = new TestDb();
        await SeedAsync(db, 1,
            Agent("v1", MessageRating.Failure), Agent("v1", MessageRating.Failure), Agent("v1", MessageRating.Good),
            Agent("v2", MessageRating.Good), Agent("v2"));

        var stats = await new StatsService(db.Context).GetAsync(CancellationToken.None);

        Assert.Equal(["v1", "v2"], stats.ByPromptVersion.Select(v => v.Version));
        var v1 = stats.ByPromptVersion[0];
        Assert.Equal((3, 3, 1, 2), (v1.AgentMessages, v1.Reviewed, v1.Good, v1.Failures));
        Assert.Equal(2.0 / 3.0, v1.FailureRate, precision: 5);
        var v2 = stats.ByPromptVersion[1];
        Assert.Equal((2, 1, 1, 0), (v2.AgentMessages, v2.Reviewed, v2.Good, v2.Failures));
        Assert.Equal(0, v2.FailureRate);
    }
}
