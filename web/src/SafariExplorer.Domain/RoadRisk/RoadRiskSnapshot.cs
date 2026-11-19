namespace SafariExplorer.Domain.RoadRisk;

public sealed record RoadRiskSnapshot(RoadRiskRun Run, IReadOnlyList<RoadSegmentRisk> Segments);
