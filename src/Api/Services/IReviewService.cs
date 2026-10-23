using Api.Models.Dtos;

namespace Api.Services;

public enum ReviewOutcome
{
    Ok,
    NotFound,
    NotAgentMessage
}

public interface IReviewService
{
    /// <summary>Grava a avaliação (Boa/Falha + comentário) numa resposta do agente. Reavaliar sobrescreve.</summary>
    Task<(ReviewOutcome Outcome, MessageDto? Message)> ReviewAsync(
        int messageId, ReviewRequest request, CancellationToken ct);
}
