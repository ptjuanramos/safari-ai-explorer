using SafariExplorer.Application.UserPaths;
using SafariExplorer.Domain.UserPaths;

namespace SafariExplorer.Api.Contracts;

/// <summary>Body of <c>POST /api/user-paths</c>. <see cref="Stops"/>: origin first, destination last.</summary>
public sealed record NewUserPathDto(
    int RouteNumber,
    string? Origin,
    string? Destination,
    IReadOnlyList<LatLngDto>? Stops,
    string? SurfaceType,
    double DistanceKm,
    int DurationMin)
{
    public NewUserPath ToDraft() => new(RouteNumber, Origin, Destination, Stops?.Select(s => s.ToPoint()).ToList(),
        SurfaceType, DistanceKm, DurationMin);
}

public sealed record UserPathDto(
    string Id,
    int RouteNumber,
    string Origin,
    string Destination,
    IReadOnlyList<LatLngDto> Stops,
    string SurfaceType,
    double DistanceKm,
    int DurationMin,
    DateTimeOffset CreatedAt)
{
    public static UserPathDto From(UserPath p) => new(p.Id, p.RouteNumber, p.Origin, p.Destination,
        p.Stops.Select(s => new LatLngDto(s.Latitude, s.Longitude)).ToList(), p.SurfaceType, p.DistanceKm,
        p.DurationMin, p.CreatedAt);
}
