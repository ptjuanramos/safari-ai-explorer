using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.Abstractions;

/// <summary>Finds the road edges that lie along each route and summarises their risk.</summary>
public interface IRouteMatcher
{
    IReadOnlyList<RouteRisk> Match(IReadOnlyList<RoadSegmentRisk> network, IReadOnlyList<TripRoute> routes);
}
