namespace SafariExplorer.Domain.RoadRisk;

public sealed record RoadRiskRun(
    string Place,
    DateOnly Date,
    string Provider,
    int LookbackDays,
    int EdgeCount,
    bool Complete,
    int? GranulesPerDay,
    IReadOnlyDictionary<DateOnly, int> GranuleCoverage,
    string Note,
    DateTimeOffset WrittenAt);
