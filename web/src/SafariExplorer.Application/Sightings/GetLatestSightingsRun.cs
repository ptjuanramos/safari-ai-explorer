using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Application.Sightings;

public sealed class GetLatestSightingsRun(ISightingsRepository repository)
{
    public Task<SightingsRun?> ExecuteAsync(CancellationToken cancellationToken = default) =>
        repository.GetLatestRunAsync(cancellationToken);
}
