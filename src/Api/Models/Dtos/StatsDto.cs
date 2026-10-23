namespace Api.Models.Dtos;

/// <summary>Resumo de qualidade do agente. FailureRate = falhas / avaliadas (0 quando nada foi avaliado).</summary>
public record StatsDto(
    int TotalConversations,
    int TotalAgentMessages,
    int ReviewedCount,
    int GoodCount,
    int FailureCount,
    double FailureRate,
    List<PromptVersionStatsDto> ByPromptVersion);

public record PromptVersionStatsDto(
    string Version,
    int AgentMessages,
    int Reviewed,
    int Good,
    int Failures,
    double FailureRate);
