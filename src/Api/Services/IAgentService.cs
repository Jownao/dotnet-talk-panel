using Api.Models.Dtos;

namespace Api.Services;

public interface IAgentService
{
    /// <summary>
    /// Salva a mensagem do aluno, pede a resposta à IA com o histórico e salva a resposta do agente.
    /// Devolve null se a conversa não existe. Lança <see cref="Ai.AiClientException"/> se a IA falhar
    /// (a mensagem do aluno continua salva; a resposta do agente não é gravada).
    /// </summary>
    Task<MessageDto?> ReplyAsync(int conversationId, string userContent, CancellationToken ct);
}
