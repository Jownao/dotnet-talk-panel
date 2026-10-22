using System.Net;
using System.Text;
using System.Text.Json;
using Api.Ai;
using Api.Models.Entities;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class GeminiClientTests
{
    private const string OkBody =
        """{"candidates":[{"content":{"parts":[{"text":"Olá, "},{"text":"tudo bem?"}],"role":"model"}}]}""";

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GeminiClient Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") },
            Options.Create(new GeminiOptions { ApiKey = "chave-de-teste", Model = "modelo-x" }));

    private static readonly IReadOnlyList<AiTurn> Turns =
    [
        new(MessageRole.User, "oi"),
        new(MessageRole.Agent, "olá!"),
        new(MessageRole.User, "quanto custa?")
    ];

    [Fact]
    public async Task Generate_Success_ReturnsConcatenatedText()
    {
        var client = Create(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, OkBody))));

        var result = await client.GenerateAsync("sistema", Turns, CancellationToken.None);

        Assert.Equal("Olá, tudo bem?", result);
    }

    [Fact]
    public async Task Generate_SendsSystemInstructionAndMappedRolesToModelEndpoint()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, OkBody)));
        var client = Create(handler);

        await client.GenerateAsync("sistema", Turns, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v1beta/models/modelo-x:generateContent", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("chave-de-teste", handler.LastRequest.Headers.GetValues("x-goog-api-key").Single());

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal("sistema", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        var contents = root.GetProperty("contents").EnumerateArray().ToList();
        Assert.Equal(["user", "model", "user"], contents.Select(c => c.GetProperty("role").GetString()));
        Assert.Equal("quanto custa?", contents[2].GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Generate_HttpError_ThrowsAiClientExceptionWithoutLeakingKey()
    {
        var client = Create(new StubHandler(_ =>
            Task.FromResult(Json(HttpStatusCode.TooManyRequests, """{"error":{"message":"quota"}}"""))));

        var ex = await Assert.ThrowsAsync<AiClientException>(
            () => client.GenerateAsync("sistema", Turns, CancellationToken.None));

        Assert.Contains("429", ex.Message);
        Assert.Contains("quota", ex.Message);
        Assert.DoesNotContain("chave-de-teste", ex.Message);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"candidates":[{"finishReason":"SAFETY"}]}""")]
    public async Task Generate_ResponseWithoutText_ThrowsAiClientException(string body)
    {
        var client = Create(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, body))));

        await Assert.ThrowsAsync<AiClientException>(
            () => client.GenerateAsync("sistema", Turns, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_NetworkError_ThrowsAiClientException()
    {
        var client = Create(new StubHandler(_ => throw new HttpRequestException("sem rede")));

        await Assert.ThrowsAsync<AiClientException>(
            () => client.GenerateAsync("sistema", Turns, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_Timeout_ThrowsAiClientException()
    {
        // O HttpClient sinaliza timeout com TaskCanceledException sem o token do chamador ter sido cancelado.
        var client = Create(new StubHandler(_ => throw new TaskCanceledException("timeout")));

        await Assert.ThrowsAsync<AiClientException>(
            () => client.GenerateAsync("sistema", Turns, CancellationToken.None));
    }

    [Fact]
    public async Task Generate_CallerCancellation_PropagatesCancellation()
    {
        var client = Create(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, OkBody))));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GenerateAsync("sistema", Turns, cts.Token));
    }
}
