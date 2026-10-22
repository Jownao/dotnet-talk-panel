using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Models.Entities;
using Microsoft.Extensions.Options;

namespace Api.Ai;

/// <summary>Fala com a API REST do Gemini (generateContent). O HttpClient vem configurado (URL base e timeout) pelo Program.cs.</summary>
public class GeminiClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;

    public GeminiClient(HttpClient http, IOptions<GeminiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken ct)
    {
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = turns.Select(t => new
            {
                role = t.Role == MessageRole.Agent ? "model" : "user",
                parts = new[] { new { text = t.Content } }
            })
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{_options.Model}:generateContent")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, ct);

            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new AiClientException(
                    $"O Gemini respondeu com status {(int)response.StatusCode}: {ExtractErrorMessage(json)}");
            }

            return ExtractText(json);
        }
        catch (HttpRequestException ex)
        {
            throw new AiClientException("Não foi possível falar com o Gemini.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // O HttpClient estoura o Timeout lançando TaskCanceledException; cancelamento do chamador passa direto.
            throw new AiClientException("O Gemini demorou demais para responder.", ex);
        }
    }

    /// <summary>Pega error.message do corpo de erro do Gemini, só para o log; se não for o formato esperado, devolve um texto neutro.</summary>
    private static string ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "sem detalhes";
            }
        }
        catch (JsonException)
        {
            // corpo que não é JSON (ex.: 404 sem corpo)
        }

        return "sem detalhes";
    }

    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts))
        {
            var text = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var t)) text.Append(t.GetString());
            }

            if (text.Length > 0) return text.ToString();
        }

        throw new AiClientException("O Gemini não devolveu texto (resposta vazia ou bloqueada).");
    }
}
