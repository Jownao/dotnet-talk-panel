namespace Api.Services;

public class AgentOptions
{
    /// <summary>Quantas mensagens mais recentes vão para a IA como contexto.</summary>
    public int HistoryLimit { get; set; } = 20;

    /// <summary>Versão ativa do prompt de sistema (arquivo Prompts/system-{versão}.txt).</summary>
    public string PromptVersion { get; set; } = "v1";
}
