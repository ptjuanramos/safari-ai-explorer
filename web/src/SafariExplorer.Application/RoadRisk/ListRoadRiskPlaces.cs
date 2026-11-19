using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.RoadRisk;

public sealed class ListRoadRiskPlaces(IRoadRiskRepository repository)
{
    /// <summary>Every place (park) with road-risk data and the rain day of its newest run.</summary>
    public async Task<IReadOnlyList<RoadRiskPlace>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var places = new List<RoadRiskPlace>();
        foreach (var place in await repository.ListPlacesAsync(cancellationToken))
        {
            if (await repository.GetLatestDateAsync(place, cancellationToken) is { } latest)
            {
                places.Add(new RoadRiskPlace(place, latest));
            }
        }

        return places;
    }
}
