using Api.Ai;
using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public class AgentService : IAgentService
{
    private readonly IMessageService _messages;
    private readonly AppDbContext _db;
    private readonly IAiClient _ai;
    private readonly IPromptProvider _prompts;
    private readonly AgentOptions _options;

    public AgentService(
        IMessageService messages,
        AppDbContext db,
        IAiClient ai,
        IPromptProvider prompts,
        IOptions<AgentOptions> options)
    {
        _messages = messages;
        _db = db;
        _ai = ai;
        _prompts = prompts;
        _options = options.Value;
    }

    public async Task<MessageDto?> ReplyAsync(int conversationId, string userContent, CancellationToken ct)
    {
        var saved = await _messages.AddUserMessageAsync(conversationId, userContent, ct);
        if (saved is null) return null;

        // As N mensagens mais recentes (já inclui a que acabou de ser salva), de volta em ordem cronológica.
        var recent = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(_options.HistoryLimit)
            .Select(m => new AiTurn(m.Role, m.Content))
            .ToListAsync(ct);
        recent.Reverse();

        // O Gemini espera que a conversa comece com um turno do usuário; o corte pode deixar um do agente na frente.
        var turns = recent.SkipWhile(t => t.Role == MessageRole.Agent).ToList();

        var version = _options.PromptVersion;
        var reply = await _ai.GenerateAsync(_prompts.GetSystemPrompt(version), turns, ct);

        var agentMessage = new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.Agent,
            Content = reply.Trim(),
            PromptVersion = version
        };
        _db.Messages.Add(agentMessage);
        await _db.SaveChangesAsync(ct);

        return MessageDto.FromEntity(agentMessage);
    }
}
