using Api.Ai;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Api.Tests;

/// <summary>
/// Sobe a API inteira em memória para testes de endpoint: banco SQLite no lugar do PostgreSQL
/// e <see cref="FakeAiClient"/> no lugar do Gemini (nenhuma chamada de rede).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public const string PromptVersion = "v-teste";

    public FakeAiClient Ai { get; } = new();

    public ApiFactory()
    {
        _connection.Open();
        // O Program.cs recusa subir sem chave; aqui ela é só um valor de teste.
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "chave-de-teste");
        // Versão de prompt fixa nos testes, independente da versão ativa no appsettings.
        Environment.SetEnvironmentVariable("Agent__PromptVersion", PromptVersion);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IAiClient>();
            services.AddSingleton<IAiClient>(Ai);

            services.RemoveAll<IPromptProvider>();
            services.AddSingleton<IPromptProvider, FakePromptProvider>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
