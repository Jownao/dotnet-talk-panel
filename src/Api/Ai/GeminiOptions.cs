namespace Api.Ai;

public class GeminiOptions
{
    /// <summary>Vem de GEMINI_API_KEY (user-secrets ou variável de ambiente), nunca do appsettings.</summary>
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gemini-3.5-flash";

    /// <summary>Precisa terminar com "/".</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    public int TimeoutSeconds { get; set; } = 20;
}
