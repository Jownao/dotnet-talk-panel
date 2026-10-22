using Api.Ai;
using Api.Models.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class AgentServiceTests
{
    private static readonly DateTime Start = DateTime.UtcNow.AddHours(-1);

    private static AgentService Create(TestDb db, FakeAiClient ai, int historyLimit = 20) =>
        new(new MessageService(db.Context),
            db.Context,
            ai,
            new FakePromptProvider(),
            Options.Create(new AgentOptions { HistoryLimit = historyLimit, PromptVersion = "v1" }));

    /// <summary>Cria aluno + conversa com mensagens já existentes (minutos após Start, em ordem).</summary>
    private static async Task<int> SeedAsync(TestDb db, params (MessageRole Role, string Text)[] history)
    {
        var student = new Student { Name = "Ana", TelegramChatId = 1, Conversation = new Conversation() };
        for (var i = 0; i < history.Length; i++)
        {
            student.Conversation.Messages.Add(new Message
            {
                Role = history[i].Role,
                Content = history[i].Text,
                CreatedAt = Start.AddMinutes(i)
            });
        }

        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        return student.Conversation.Id;
    }

    [Fact]
    public async Task Reply_BuildsSystemPromptAndTurnsInChronologicalOrderWithRoles()
    {
        using var db = new TestDb();
        var id = await SeedAsync(db, (MessageRole.User, "oi"), (MessageRole.Agent, "olá!"));
        var ai = new FakeAiClient();

        await Create(db, ai).ReplyAsync(id, "quanto custa?", CancellationToken.None);

        Assert.Equal("SISTEMA-v1", ai.LastSystemPrompt);
        Assert.Equal(
            [new AiTurn(MessageRole.User, "oi"), new AiTurn(MessageRole.Agent, "olá!"), new AiTurn(MessageRole.User, "quanto custa?")],
            ai.LastTurns);
    }

    [Fact]
    public async Task Reply_CutsHistoryToLimitKeepingMostRecent()
    {
        using var db = new TestDb();
        var id = await SeedAsync(db,
            (MessageRole.User, "u1"), (MessageRole.Agent, "a2"), (MessageRole.User, "u3"), (MessageRole.Agent, "a4"));
        var ai = new FakeAiClient();

        await Create(db, ai, historyLimit: 3).ReplyAsync(id, "u5", CancellationToken.None);

        Assert.Equal(["u3", "a4", "u5"], ai.LastTurns.Select(t => t.Content));
    }

    [Fact]
    public async Task Reply_AfterCut_NeverStartsWithAgentTurn()
    {
        using var db = new TestDb();
        var id = await SeedAsync(db,
            (MessageRole.User, "u1"), (MessageRole.Agent, "a2"), (MessageRole.User, "u3"), (MessageRole.Agent, "a4"));
        var ai = new FakeAiClient();

        await Create(db, ai, historyLimit: 2).ReplyAsync(id, "u5", CancellationToken.None);

        // O corte deixaria [a4, u5]; o turno do agente inicial é descartado.
        Assert.Equal(["u5"], ai.LastTurns.Select(t => t.Content));
    }

    [Fact]
    public async Task Reply_SavesAgentMessageWithPromptVersionAndReturnsIt()
    {
        using var db = new TestDb();
        var id = await SeedAsync(db);
        var ai = new FakeAiClient { Reply = "  O curso custa R$ 390,00.  " };

        var result = await Create(db, ai).ReplyAsync(id, "quanto custa?", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(MessageRole.Agent, result.Role);
        Assert.Equal("O curso custa R$ 390,00.", result.Content);
        Assert.Equal("v1", result.PromptVersion);

        var saved = await db.Context.Messages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal([MessageRole.User, MessageRole.Agent], saved.Select(m => m.Role));
        Assert.Equal("v1", saved[1].PromptVersion);
        Assert.Null(saved[0].PromptVersion);
    }

    [Fact]
    public async Task Reply_WhenAiFails_KeepsUserMessageAndSavesNoAgentMessage()
    {
        using var db = new TestDb();
        var id = await SeedAsync(db);
        var ai = new FakeAiClient { ExceptionToThrow = new AiClientException("caiu") };

        await Assert.ThrowsAsync<AiClientException>(
            () => Create(db, ai).ReplyAsync(id, "oi", CancellationToken.None));

        var saved = await db.Context.Messages.AsNoTracking().SingleAsync();
        Assert.Equal(MessageRole.User, saved.Role);
        Assert.Equal("oi", saved.Content);
    }

    [Fact]
    public async Task Reply_UnknownConversation_ReturnsNullWithoutCallingAi()
    {
        using var db = new TestDb();
        var ai = new FakeAiClient();

        var result = await Create(db, ai).ReplyAsync(999, "oi", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, ai.Calls);
    }
}
