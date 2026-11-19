using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Api.Contracts;

public sealed record SpeciesSummaryDto(string Name, long SpeciesKey, int Records, double P0, double Vmax);

public sealed record LayerGridDto(double X0, double Y0, int Nx, int Ny);

public sealed record TileRefDto(int Tx, int Ty, IReadOnlyList<double> BoundsWgs84);

public sealed record SightingsLayerDto(
    int SigmaM,
    double CellM,
    int TileCells,
    double? MaxUncertaintyM,
    int Records,
    IReadOnlyDictionary<string, int> SpeciesRecords,
    IReadOnlyDictionary<string, double> SpeciesP0,
    LayerGridDto Grid,
    IReadOnlyList<TileRefDto> Tiles)
{
    public static SightingsLayerDto From(SightingsLayer l) => new(
        l.SigmaM, l.CellM, l.TileCells, l.MaxUncertaintyM, l.Records, l.SpeciesRecords, l.SpeciesP0,
        new LayerGridDto(l.Grid.X0, l.Grid.Y0, l.Grid.Nx, l.Grid.Ny),
        l.Tiles.Select(t => new TileRefDto(t.Tx, t.Ty, t.BoundsWgs84)).ToList());
}

public sealed record SightingsRunDto(
    DateOnly Date,
    int StartYear,
    int EndYear,
    int Records,
    IReadOnlyList<SightingsLayerDto> Layers,
    IReadOnlyList<SpeciesSummaryDto> Species,
    string Meaning,
    DateTimeOffset WrittenAt)
{
    public static SightingsRunDto From(SightingsRun run) => new(
        run.Date, run.StartYear, run.EndYear, run.Records,
        run.Layers.Select(SightingsLayerDto.From).ToList(),
        run.Species.Select(s => new SpeciesSummaryDto(s.Name, s.SpeciesKey, s.Records, s.P0, s.Vmax)).ToList(),
        run.Meaning, run.WrittenAt);
}

public sealed record SightingsTileDto(
    string Species,
    string Crs,
    int SigmaM,
    double CellM,
    int Tx,
    int Ty,
    double X0,
    double Y0,
    int Nx,
    int Ny,
    IReadOnlyList<double> BoundsWgs84,
    double P0,
    double ConfidenceRecords,
    IReadOnlyList<double?> Share,
    IReadOnlyList<double> N,
    IReadOnlyList<double?> Lo,
    IReadOnlyList<double?> Hi)
{
    public static SightingsTileDto From(SightingsTile t) => new(
        t.Species, t.Crs, t.SigmaM, t.CellM, t.Tx, t.Ty, t.X0, t.Y0, t.Nx, t.Ny, t.BoundsWgs84, t.P0,
        t.ConfidenceRecords, t.Share, t.N, t.Lo, t.Hi);
}
