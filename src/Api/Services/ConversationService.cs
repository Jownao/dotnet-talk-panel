using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ConversationService : IConversationService
{
    private readonly AppDbContext _db;

    public ConversationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(ConversationDto Conversation, bool Created)> GetOrCreateAsync(
        CreateConversationRequest request, CancellationToken ct)
    {
        var existing = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.Student.TelegramChatId == request.TelegramChatId, ct);

        if (existing is not null)
        {
            return (ToDto(existing), false);
        }

        var student = new Student
        {
            Name = request.StudentName,
            TelegramChatId = request.TelegramChatId,
            Conversation = new Conversation()
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);

        student.Conversation.Student = student;
        return (ToDto(student.Conversation), true);
    }

    public async Task<List<ConversationSummaryDto>> ListAsync(bool hasFailures, CancellationToken ct)
    {
        var query = _db.Conversations.AsNoTracking();

        if (hasFailures)
        {
            query = query.Where(c => c.Messages.Any(m => m.Rating == MessageRating.Failure));
        }

        var rows = await query
            .Select(c => new
            {
                c.Id,
                StudentName = c.Student.Name,
                c.CreatedAt,
                LastContent = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                LastAt = c.Messages.Max(m => (DateTime?)m.CreatedAt),
                FailureCount = c.Messages.Count(m => m.Rating == MessageRating.Failure)
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ConversationSummaryDto(
                r.Id, r.StudentName, Preview(r.LastContent), r.LastAt ?? r.CreatedAt, r.FailureCount))
            .OrderByDescending(r => r.LastActivityAt)
            .ToList();
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Student)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null) return null;

        return new ConversationDetailDto(
            conversation.Id,
            conversation.StudentId,
            conversation.Student.Name,
            conversation.CreatedAt,
            conversation.Messages.Select(MessageDto.FromEntity).ToList());
    }

    private static string? Preview(string? content)
    {
        const int maxLength = 80;
        if (content is null) return null;
        return content.Length <= maxLength ? content : content[..maxLength] + "…";
    }

    private static ConversationDto ToDto(Conversation c) =>
        new(c.Id, c.StudentId, c.Student.Name, c.CreatedAt);
}
