using SafariExplorer.Domain.Directions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.Abstractions;

/// <summary>Driving directions from an external routing service.</summary>
public interface IDirectionsService
{
    /// <summary>False when the service has no credentials, so callers can say so instead of failing.</summary>
    bool IsConfigured { get; }

    /// <summary>The driving route through the points, or null when the service finds none.</summary>
    Task<DrivingRoute?> GetRouteAsync(GeoPoint origin, GeoPoint destination, IReadOnlyList<GeoPoint> via,
        CancellationToken cancellationToken);
}
