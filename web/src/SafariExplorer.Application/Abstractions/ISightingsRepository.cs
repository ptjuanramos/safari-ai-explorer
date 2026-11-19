using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Application.Abstractions;

public interface ISightingsRepository
{
    Task<SightingsRun?> GetLatestRunAsync(CancellationToken cancellationToken);

    Task<SightingsTile?> GetTileAsync(DateOnly runDate, string species, int sigmaM, int tx, int ty,
        CancellationToken cancellationToken);
}
