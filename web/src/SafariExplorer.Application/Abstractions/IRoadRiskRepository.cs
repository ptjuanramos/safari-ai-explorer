using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.Abstractions;

public interface IRoadRiskRepository
{
    /// <summary>Slugs of the places (parks) that have a latest run, in name order.</summary>
    Task<IReadOnlyList<string>> ListPlacesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DateOnly>> ListDatesAsync(string place, CancellationToken cancellationToken);

    Task<DateOnly?> GetLatestDateAsync(string place, CancellationToken cancellationToken);

    Task<RoadRiskSnapshot?> GetAsync(string place, DateOnly date, CancellationToken cancellationToken);
}
