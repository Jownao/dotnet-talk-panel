using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class StatsService : IStatsService
{
    private readonly AppDbContext _db;

    public StatsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StatsDto> GetAsync(CancellationToken ct)
    {
        var totalConversations = await _db.Conversations.CountAsync(ct);

        // Uma consulta agrupada por versão do prompt; os totais gerais saem da soma dos grupos.
        var groups = await _db.Messages
            .AsNoTracking()
            .Where(m => m.Role == MessageRole.Agent)
            .GroupBy(m => m.PromptVersion)
            .Select(g => new
            {
                Version = g.Key,
                AgentMessages = g.Count(),
                Good = g.Count(m => m.Rating == MessageRating.Good),
                Failures = g.Count(m => m.Rating == MessageRating.Failure)
            })
            .ToListAsync(ct);

        var byVersion = groups
            .OrderBy(g => g.Version)
            .Select(g => new PromptVersionStatsDto(
                g.Version ?? "sem-versao",
                g.AgentMessages,
                g.Good + g.Failures,
                g.Good,
                g.Failures,
                Rate(g.Failures, g.Good + g.Failures)))
            .ToList();

        var good = groups.Sum(g => g.Good);
        var failures = groups.Sum(g => g.Failures);

        return new StatsDto(
            totalConversations,
            groups.Sum(g => g.AgentMessages),
            good + failures,
            good,
            failures,
            Rate(failures, good + failures),
            byVersion);
    }

    private static double Rate(int failures, int reviewed) => reviewed == 0 ? 0 : (double)failures / reviewed;
}
