using Api.Services;

namespace Api.Tests;

public sealed class FilePromptProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "prompts-" + Guid.NewGuid());

    public FilePromptProviderTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "system-v1.txt"), "Você é a atendente.");
        File.WriteAllText(Path.Combine(_dir, "knowledge-base.txt"), "Curso de inglês custa R$ 100.");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void GetSystemPrompt_JoinsSystemPromptAndKnowledgeBase()
    {
        var provider = new FilePromptProvider(_dir);

        var prompt = provider.GetSystemPrompt("v1");

        Assert.Equal("Você é a atendente.\n\n## Base de conhecimento\nCurso de inglês custa R$ 100.", prompt);
    }

    [Fact]
    public void GetSystemPrompt_UnknownVersion_Throws()
    {
        var provider = new FilePromptProvider(_dir);

        Assert.Throws<InvalidOperationException>(() => provider.GetSystemPrompt("v9"));
    }

    [Theory]
    [InlineData("../system-v1")]
    [InlineData("v1/../v1")]
    [InlineData("")]
    public void GetSystemPrompt_InvalidVersionName_Throws(string version)
    {
        var provider = new FilePromptProvider(_dir);

        Assert.Throws<InvalidOperationException>(() => provider.GetSystemPrompt(version));
    }
}
