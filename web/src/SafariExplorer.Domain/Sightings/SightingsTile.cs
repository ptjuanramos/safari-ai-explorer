namespace SafariExplorer.Domain.Sightings;

public sealed record SightingsTile(
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
    IReadOnlyList<double?> Hi);
