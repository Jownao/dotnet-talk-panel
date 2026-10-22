using Api.Models.Dtos;

namespace Api.Services;

public interface IConversationService
{
    /// <summary>Acha a conversa do aluno pelo chatId do Telegram ou cria aluno + conversa. Created = true se criou.</summary>
    Task<(ConversationDto Conversation, bool Created)> GetOrCreateAsync(
        CreateConversationRequest request, CancellationToken ct);

    /// <summary>Lista conversas, mais recentes primeiro. Com hasFailures, só as que têm resposta marcada como Failure.</summary>
    Task<List<ConversationSummaryDto>> ListAsync(bool hasFailures, CancellationToken ct);

    /// <summary>Conversa com todas as mensagens em ordem cronológica, ou null se não existir.</summary>
    Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct);
}
