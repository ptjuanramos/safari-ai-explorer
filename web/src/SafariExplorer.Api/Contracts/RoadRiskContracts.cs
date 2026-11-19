using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Api.Contracts;

public sealed record RoadRiskRunDto(
    string Place,
    DateOnly Date,
    string Provider,
    int LookbackDays,
    int EdgeCount,
    bool Complete,
    int? GranulesPerDay,
    IReadOnlyDictionary<DateOnly, int> GranuleCoverage,
    string Note,
    DateTimeOffset WrittenAt)
{
    public static RoadRiskRunDto From(RoadRiskRun run) => new(
        run.Place, run.Date, run.Provider, run.LookbackDays, run.EdgeCount, run.Complete, run.GranulesPerDay,
        run.GranuleCoverage, run.Note, run.WrittenAt);
}

public sealed record LineStringDto(IReadOnlyList<double[]> Coordinates)
{
    public string Type => "LineString";
}

public sealed record RoadPropertiesDto(
    string RoadType,
    double LengthM,
    double TravelTimeMin,
    double? RainIndexMm,
    double SurfaceRisk);

public sealed record RoadFeatureDto(string Id, LineStringDto Geometry, RoadPropertiesDto Properties)
{
    public string Type => "Feature";
}

public sealed record RoadRiskFeatureCollectionDto(RoadRiskRunDto Run, IReadOnlyList<RoadFeatureDto> Features)
{
    public string Type => "FeatureCollection";

    public static RoadRiskFeatureCollectionDto From(RoadRiskSnapshot snapshot) => new(
        RoadRiskRunDto.From(snapshot.Run),
        snapshot.Segments.Select(s => new RoadFeatureDto(
            s.Id,
            new LineStringDto(s.Line.Select(p => new[] { p.Longitude, p.Latitude }).ToList()),
            new RoadPropertiesDto(s.RoadType, s.LengthM, s.TravelTimeMin, s.RainIndexMm, s.SurfaceRisk))).ToList());
}

public sealed record RoadRiskDatesDto(string Place, IReadOnlyList<DateOnly> Dates);

public sealed record LatLngDto(double Lat, double Lng)
{
    public GeoPoint ToPoint() => new(Lng, Lat);
}

public sealed record RouteRequestDto(string Id, IReadOnlyList<LatLngDto> Path);

/// <summary>Body of <c>POST /api/road-risk/routes</c>: the trip date and the routes to score.</summary>
public sealed record RouteRiskRequestDto(DateOnly Date, IReadOnlyList<RouteRequestDto> Routes)
{
    public const int MaxRoutes = 50;
    public const int MaxPointsPerRoute = 20_000;

    public string? Validate() =>
        Routes is not { Count: > 0 and <= MaxRoutes } ? $"send between 1 and {MaxRoutes} routes"
        : Routes.Any(r => string.IsNullOrWhiteSpace(r.Id)) ? "every route needs an id"
        : Routes.Any(r => r.Path is not { Count: >= 2 and <= MaxPointsPerRoute })
            ? $"every route needs between 2 and {MaxPointsPerRoute} points"
        : null;

    public IReadOnlyList<TripRoute> ToRoutes() =>
        Routes.Select(r => new TripRoute(r.Id, r.Path.Select(p => p.ToPoint()).ToList())).ToList();
}

public sealed record RouteRiskDto(
    string Id,
    double RouteLengthM,
    double MatchedLengthM,
    double Coverage,
    int MatchedEdges,
    double? MeanSurfaceRisk,
    double? MaxSurfaceRisk,
    double? MaxRainIndexMm,
    IReadOnlyList<string> RoadTypes,
    IReadOnlyList<string> Places)
{
    public static RouteRiskDto From(RouteRisk r) => new(
        r.Id, Math.Round(r.RouteLengthM), Math.Round(r.MatchedLengthM), Math.Round(r.Coverage, 3), r.MatchedEdges,
        r.MeanSurfaceRisk, r.MaxSurfaceRisk, r.MaxRainIndexMm, r.RoadTypes, r.Places);
}

public sealed record RouteRiskReportDto(
    IReadOnlyList<RoadRiskRunDto> Runs, DateOnly RequestedDate, IReadOnlyList<RouteRiskDto> Routes)
{
    public static RouteRiskReportDto From(RouteRiskReport report) => new(
        report.Runs.Select(RoadRiskRunDto.From).ToList(), report.RequestedDate,
        report.Routes.Select(RouteRiskDto.From).ToList());
}

public sealed record RoadRiskPlaceDto(string Place, DateOnly LatestDate)
{
    public static RoadRiskPlaceDto From(RoadRiskPlace p) => new(p.Place, p.LatestDate);
}
