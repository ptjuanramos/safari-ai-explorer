using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.UserPaths;
using SafariExplorer.Infrastructure.JobData;

namespace SafariExplorer.Infrastructure.FileSystem;

/// <summary>
/// One JSON file per path in <c>DATA_DIR/user-paths/&lt;id&gt;.json</c> (ADR-0003). Each file is written to a temp
/// file and renamed, so a reader never sees half a file. Ids must be slugs, which keeps them inside the folder.
/// </summary>
public sealed class FileUserPathRepository(IOptions<DataOptions> options) : IUserPathRepository
{
    public const string Folder = "user-paths";
    /// <summary>2: <c>route_number</c>. Version 1 files held <c>day_number</c> and are still read.</summary>
    private const int FormatVersion = 2;

    private string Dir => Path.Combine(options.Value.Root, Folder);

    public async Task<IReadOnlyList<UserPath>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Dir))
        {
            return [];
        }

        var paths = new List<UserPath>();
        foreach (var file in Directory.EnumerateFiles(Dir, "*.json"))
        {
            if (!JobFiles.Slug().IsMatch(Path.GetFileNameWithoutExtension(file)))
            {
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(file);
                if (await JsonSerializer.DeserializeAsync<UserPathFile>(stream, JobFiles.Json, cancellationToken) is { } saved)
                {
                    paths.Add(ToPath(saved));
                }
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                // A hand-edited file that no longer parses hides that one path, not the whole trip.
            }
        }

        return paths;
    }

    public async Task SaveAsync(UserPath path, CancellationToken cancellationToken)
    {
        var file = FileFor(path.Id) ?? throw new ArgumentException($"'{path.Id}' is not a valid path id", nameof(path));
        Directory.CreateDirectory(Dir);
        var temp = Path.Combine(Dir, $".{path.Id}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, ToFile(path), JobFiles.Json, cancellationToken);
            }

            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (FileFor(id) is not { } file || !File.Exists(file))
        {
            return Task.FromResult(false);
        }

        File.Delete(file);
        return Task.FromResult(true);
    }

    private string? FileFor(string id) => JobFiles.Slug().IsMatch(id) ? Path.Combine(Dir, $"{id}.json") : null;

    private static UserPathFile ToFile(UserPath p) => new(
        FormatVersion, p.Id, p.RouteNumber, p.Origin, p.Destination,
        p.Stops.Select(s => new StopFile(s.Latitude, s.Longitude)).ToList(),
        p.SurfaceType, p.DistanceKm, p.DurationMin, p.CreatedAt);

    private static UserPath ToPath(UserPathFile f) => new(
        f.Id, f.RouteNumber ?? f.DayNumber ?? throw new InvalidDataException("route_number is missing"),
        f.Origin, f.Destination,
        f.Stops.Select(s => new GeoPoint(s.Lng, s.Lat)).ToList(),
        f.SurfaceType, f.DistanceKm, f.DurationMin, f.CreatedAt);
}

internal sealed record UserPathFile(
    int Version,
    string Id,
    int? RouteNumber,
    string Origin,
    string Destination,
    List<StopFile> Stops,
    string SurfaceType,
    double DistanceKm,
    int DurationMin,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DayNumber = null);

internal sealed record StopFile(double Lat, double Lng);
