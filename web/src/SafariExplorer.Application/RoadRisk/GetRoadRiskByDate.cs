using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.RoadRisk;

public sealed class GetRoadRiskByDate(IRoadRiskRepository repository)
{
    public Task<RoadRiskSnapshot?> ExecuteAsync(string place, DateOnly date, CancellationToken cancellationToken = default) =>
        repository.GetAsync(place, date, cancellationToken);
}
