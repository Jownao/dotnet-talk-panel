using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Ai;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

public class ApiEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static long _nextChatId = 1000;

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<int> CreateConversationAsync(string name = "Ana")
    {
        var chatId = Interlocked.Increment(ref _nextChatId);
        var response = await _client.PostAsJsonAsync("/api/conversations", new { telegramChatId = chatId, studentName = name });
        var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>(Json);
        return conversation!.Id;
    }

    private async Task<MessageDto> SendAsync(int conversationId, string content)
    {
        var response = await _client.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new { content });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageDto>(Json))!;
    }

    private Task<HttpResponseMessage> ReviewAsync(int messageId, object body) =>
        _client.PatchAsJsonAsync($"/api/messages/{messageId}/review", body);

    [Fact]
    public async Task CreateConversation_FirstTime201_ThenSameChatId200WithSameId()
    {
        var body = new { telegramChatId = Interlocked.Increment(ref _nextChatId), studentName = "Ana" };

        var first = await _client.PostAsJsonAsync("/api/conversations", body);
        var second = await _client.PostAsJsonAsync("/api/conversations", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<ConversationDto>(Json);
        var b = await second.Content.ReadFromJsonAsync<ConversationDto>(Json);
        Assert.Equal(a!.Id, b!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateConversation_BlankStudentName_Returns400(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/conversations", new { telegramChatId = Interlocked.Increment(ref _nextChatId), studentName = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetConversation_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/api/conversations/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_ReturnsAgentReply_AndConversationHoldsBothMessages()
    {
        _factory.Ai.Reply = "O curso custa R$ 390,00 por mês.";
        var id = await CreateConversationAsync();

        var reply = await SendAsync(id, "Quanto custa?");

        Assert.Equal(MessageRole.Agent, reply.Role);
        Assert.Equal("O curso custa R$ 390,00 por mês.", reply.Content);
        Assert.Equal(ApiFactory.PromptVersion, reply.PromptVersion);

        var detail = await _client.GetFromJsonAsync<ConversationDetailDto>($"/api/conversations/{id}", Json);
        Assert.Equal([MessageRole.User, MessageRole.Agent], detail!.Messages.Select(m => m.Role));
        Assert.Equal("Quanto custa?", detail.Messages[0].Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessage_BlankContent_Returns400(string content)
    {
        var id = await CreateConversationAsync();

        var response = await _client.PostAsJsonAsync($"/api/conversations/{id}/messages", new { content });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_TooLongContent_Returns400()
    {
        var id = await CreateConversationAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{id}/messages", new { content = new string('a', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_UnknownConversation_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/conversations/987654/messages", new { content = "oi" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_WhenAiFails_Returns502ProblemDetailsAndKeepsStudentMessage()
    {
        var id = await CreateConversationAsync();
        _factory.Ai.ExceptionToThrow = new AiClientException("caiu");
        try
        {
            var response = await _client.PostAsJsonAsync($"/api/conversations/{id}/messages", new { content = "oi" });

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
            Assert.Equal(502, problem!.Status);
        }
        finally
        {
            _factory.Ai.ExceptionToThrow = null;
        }

        var detail = await _client.GetFromJsonAsync<ConversationDetailDto>($"/api/conversations/{id}", Json);
        var only = Assert.Single(detail!.Messages);
        Assert.Equal(MessageRole.User, only.Role);
    }

    [Fact]
    public async Task Review_AgentMessage_Returns200_AndShowsUpInFailureFilterAndStats()
    {
        var id = await CreateConversationAsync("Revisada");
        var reply = await SendAsync(id, "oi");

        var response = await ReviewAsync(reply.Id, new { rating = "Failure", comment = "Respondeu fora do tema." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reviewed = await response.Content.ReadFromJsonAsync<MessageDto>(Json);
        Assert.Equal(MessageRating.Failure, reviewed!.Rating);
        Assert.Equal("Respondeu fora do tema.", reviewed.ReviewComment);

        var failures = await _client.GetFromJsonAsync<List<ConversationSummaryDto>>(
            "/api/conversations?hasFailures=true", Json);
        Assert.Contains(failures!, c => c.Id == id && c.FailureCount == 1);

        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/stats", Json);
        Assert.True(stats!.FailureCount >= 1);
        Assert.Contains(stats.ByPromptVersion, v => v.Version == ApiFactory.PromptVersion && v.Failures >= 1);
    }

    [Fact]
    public async Task Review_UserMessage_Returns400()
    {
        var id = await CreateConversationAsync();
        await SendAsync(id, "oi");
        var detail = await _client.GetFromJsonAsync<ConversationDetailDto>($"/api/conversations/{id}", Json);
        var userMessage = detail!.Messages.First(m => m.Role == MessageRole.User);

        var response = await ReviewAsync(userMessage.Id, new { rating = "Good" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"rating":"Otimo"}""")]
    [InlineData("""{"rating":99}""")]
    [InlineData("""{"comment":"sem nota"}""")]
    public async Task Review_InvalidOrMissingRating_Returns400(string json)
    {
        var id = await CreateConversationAsync();
        var reply = await SendAsync(id, "oi");

        var response = await _client.PatchAsync(
            $"/api/messages/{reply.Id}/review",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Review_UnknownMessage_Returns404()
    {
        var response = await ReviewAsync(987654, new { rating = "Good" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_AllowsPanelOrigin_AndBlocksOthers()
    {
        HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/messages/1/review");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "PATCH");
            return request;
        }

        var allowed = await _client.SendAsync(Preflight("http://localhost:5173"));
        var blocked = await _client.SendAsync(Preflight("http://evil.example"));

        Assert.Equal("http://localhost:5173", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(blocked.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
