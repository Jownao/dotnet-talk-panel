using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(ReviewOutcome Outcome, MessageDto? Message)> ReviewAsync(
        int messageId, ReviewRequest request, CancellationToken ct)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null) return (ReviewOutcome.NotFound, null);

        // Só respostas do agente são avaliadas; mensagens do aluno não.
        if (message.Role != MessageRole.Agent) return (ReviewOutcome.NotAgentMessage, null);

        message.Rating = request.Rating ?? throw new ArgumentException("Rating é obrigatório.", nameof(request));
        message.ReviewComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        message.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (ReviewOutcome.Ok, MessageDto.FromEntity(message));
    }
}
