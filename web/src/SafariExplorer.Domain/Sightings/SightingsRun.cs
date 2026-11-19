namespace SafariExplorer.Domain.Sightings;

public sealed record SpeciesSummary(string Name, long SpeciesKey, int Records, double P0, double Vmax);

public sealed record LayerGrid(double X0, double Y0, int Nx, int Ny);

public sealed record TileRef(int Tx, int Ty, IReadOnlyList<double> BoundsWgs84);

public sealed record SightingsLayer(
    int SigmaM,
    double CellM,
    int TileCells,
    double? MaxUncertaintyM,
    int Records,
    IReadOnlyDictionary<string, int> SpeciesRecords,
    IReadOnlyDictionary<string, double> SpeciesP0,
    LayerGrid Grid,
    IReadOnlyList<TileRef> Tiles)
{
    public bool HasTile(int tx, int ty) => Tiles.Any(t => t.Tx == tx && t.Ty == ty);
}

public sealed record SightingsRun(
    DateOnly Date,
    int StartYear,
    int EndYear,
    int Records,
    IReadOnlyList<SightingsLayer> Layers,
    IReadOnlyList<SpeciesSummary> Species,
    string Meaning,
    DateTimeOffset WrittenAt)
{
    public bool HasTile(string species, int sigmaM, int tx, int ty) =>
        Species.Any(s => s.Name == species)
        && Layers.FirstOrDefault(l => l.SigmaM == sigmaM)?.HasTile(tx, ty) == true;
}
