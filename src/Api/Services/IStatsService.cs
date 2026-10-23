using Api.Models.Dtos;

namespace Api.Services;

public interface IStatsService
{
    Task<StatsDto> GetAsync(CancellationToken ct);
}
