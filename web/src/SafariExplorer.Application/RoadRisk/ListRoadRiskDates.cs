using SafariExplorer.Application.Abstractions;

namespace SafariExplorer.Application.RoadRisk;

public sealed class ListRoadRiskDates(IRoadRiskRepository repository)
{
    public Task<IReadOnlyList<DateOnly>> ExecuteAsync(string place, CancellationToken cancellationToken = default) =>
        repository.ListDatesAsync(place, cancellationToken);
}
