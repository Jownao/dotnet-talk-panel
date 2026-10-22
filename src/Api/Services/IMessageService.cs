using Api.Models.Dtos;

namespace Api.Services;

public interface IMessageService
{
    /// <summary>Salva a mensagem do aluno. Devolve null se a conversa não existe.</summary>
    Task<MessageDto?> AddUserMessageAsync(int conversationId, string content, CancellationToken ct);
}
