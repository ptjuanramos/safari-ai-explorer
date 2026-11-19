using System.Text.Json;

namespace SafariExplorer.Infrastructure.JobData;

internal sealed record RoadRiskManifest(
    string Job,
    string Place,
    string Date,
    string Provider,
    int LookbackDays,
    int EdgeCount,
    bool Complete,
    int? GranulesPerDay,
    Dictionary<string, int>? GranuleCoverage,
    string Note,
    DateTimeOffset WrittenAt);

internal sealed record EdgeCollection(List<EdgeFeature> Features);

internal sealed record EdgeFeature(string Id, EdgeProperties Properties, EdgeGeometry Geometry);

internal sealed record EdgeProperties(
    string RoadType,
    double LengthM,
    double TravelTimeM,
    double? RainMm,
    double SurfaceRisk);

internal sealed record EdgeGeometry(string Type, JsonElement Coordinates);
