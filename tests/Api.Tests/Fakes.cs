using Api.Ai;
using Api.Services;

namespace Api.Tests;

/// <summary>HttpMessageHandler falso: guarda a requisição e devolve o que o teste mandar.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }

    public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return await _respond(request);
    }
}

/// <summary>IAiClient falso: registra o que recebeu e devolve (ou lança) o que o teste configurar.</summary>
public sealed class FakeAiClient : IAiClient
{
    public string Reply { get; set; } = "resposta do agente";
    public Exception? ExceptionToThrow { get; set; }

    public int Calls { get; private set; }
    public string? LastSystemPrompt { get; private set; }
    public List<AiTurn> LastTurns { get; private set; } = [];

    public Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken ct)
    {
        Calls++;
        LastSystemPrompt = systemPrompt;
        LastTurns = turns.ToList();

        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        return Task.FromResult(Reply);
    }
}

public sealed class FakePromptProvider : IPromptProvider
{
    public string GetSystemPrompt(string version) => $"SISTEMA-{version}";
}
