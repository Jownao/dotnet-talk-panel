using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class MessageService : IMessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MessageDto?> AddUserMessageAsync(int conversationId, string content, CancellationToken ct)
    {
        var exists = await _db.Conversations.AnyAsync(c => c.Id == conversationId, ct);
        if (!exists) return null;

        var message = new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = content.Trim()
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        return MessageDto.FromEntity(message);
    }
}
