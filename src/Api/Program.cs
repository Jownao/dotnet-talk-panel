using System.Text.Json.Serialization;
using Api.Ai;
using Api.Data;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Gemini: modelo, URL e timeout vêm do appsettings; a chave vem só de GEMINI_API_KEY (user-secrets ou variável de ambiente).
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.PostConfigure<GeminiOptions>(o => o.ApiKey = builder.Configuration["GEMINI_API_KEY"] ?? "");
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

builder.Services.AddHttpClient<IAiClient, GeminiClient>((sp, http) =>
{
    var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    http.BaseAddress = new Uri(gemini.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(gemini.TimeoutSeconds);
});

builder.Services.AddSingleton<IPromptProvider>(
    new FilePromptProvider(Path.Combine(AppContext.BaseDirectory, "Prompts")));

builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IStatsService, StatsService>();

// CORS: o painel React (Vite) roda em outra origem e precisa ser liberado explicitamente.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Panel", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Falha cedo e com mensagem clara se a chave não existe. Fica depois do Build() para não atrapalhar o `dotnet ef`.
if (string.IsNullOrWhiteSpace(app.Configuration["GEMINI_API_KEY"]))
{
    throw new InvalidOperationException(
        "GEMINI_API_KEY não configurada. Em desenvolvimento: dotnet user-secrets set \"GEMINI_API_KEY\" \"<sua chave>\" --project src/Api");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Em desenvolvimento, aplica as migrations ao subir (RNF-05).
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors("Panel");

app.MapControllers();

app.Run();
