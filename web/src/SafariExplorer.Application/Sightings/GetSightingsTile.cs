using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Application.Sightings;

public sealed class GetSightingsTile(ISightingsRepository repository)
{
    public async Task<SightingsTile?> ExecuteAsync(string species, int sigmaM, int tx, int ty,
        CancellationToken cancellationToken = default)
    {
        var run = await repository.GetLatestRunAsync(cancellationToken);
        if (run is null || !run.HasTile(species, sigmaM, tx, ty))
        {
            return null;
        }

        return await repository.GetTileAsync(run.Date, species, sigmaM, tx, ty, cancellationToken);
    }
}
