namespace Api.Services;

public interface IPromptProvider
{
    /// <summary>Prompt de sistema completo (instruções + base de conhecimento) da versão pedida, ex.: "v1".</summary>
    string GetSystemPrompt(string version);
}
