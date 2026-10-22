using Api.Models.Entities;

namespace Api.Ai;

/// <summary>Um turno da conversa enviado à IA.</summary>
public record AiTurn(MessageRole Role, string Content);

/// <summary>Isola o provedor de IA: o resto do sistema só conhece esta interface.</summary>
public interface IAiClient
{
    /// <summary>Gera a próxima resposta. Lança <see cref="AiClientException"/> se a IA falhar ou estourar o tempo.</summary>
    Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken ct);
}
