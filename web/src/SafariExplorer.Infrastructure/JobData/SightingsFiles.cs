namespace SafariExplorer.Infrastructure.JobData;

internal sealed record SightingsManifest(
    string Job,
    string Date,
    List<int> Years,
    int Records,
    List<LayerEntry> Layers,
    Dictionary<string, SpeciesEntry> Species,
    string Meaning,
    DateTimeOffset WrittenAt);

internal sealed record LayerEntry(
    int SigmaM,
    double CellM,
    int TileCells,
    double? MaxUncertaintyM,
    int Records,
    Dictionary<string, int> SpeciesRecords,
    Dictionary<string, double> SpeciesP0,
    GridEntry Grid,
    List<TileEntry> Tiles);

internal sealed record GridEntry(double X0, double Y0, int Nx, int Ny);

internal sealed record TileEntry(int Tx, int Ty, List<double> BoundsWgs84);

internal sealed record SpeciesEntry(long SpeciesKey, int Records, double P0, double Vmax);

internal sealed record TileFile(
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
    List<double> BoundsWgs84,
    double P0,
    double ConfidenceRecords,
    List<double?> Share,
    List<double> N,
    List<double?> Lo,
    List<double?> Hi);
