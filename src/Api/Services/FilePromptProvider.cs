using System.Text.RegularExpressions;

namespace Api.Services;

/// <summary>Lê o prompt de sistema versionado (Prompts/system-vN.txt) e a base de conhecimento do disco.</summary>
public partial class FilePromptProvider : IPromptProvider
{
    private readonly string _directory;

    public FilePromptProvider(string promptsDirectory)
    {
        _directory = promptsDirectory;
    }

    public string GetSystemPrompt(string version)
    {
        // Só letras minúsculas, números e hífen: impede "../" e outros caminhos.
        if (!ValidVersion().IsMatch(version))
        {
            throw new InvalidOperationException($"Versão de prompt inválida: '{version}'.");
        }

        var system = ReadFile($"system-{version}.txt");
        var knowledgeBase = ReadFile("knowledge-base.txt");

        return $"{system}\n\n## Base de conhecimento\n{knowledgeBase}";
    }

    private string ReadFile(string name)
    {
        var path = Path.Combine(_directory, name);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Arquivo de prompt não encontrado: {name}");
        }

        return File.ReadAllText(path).Trim();
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex ValidVersion();
}
