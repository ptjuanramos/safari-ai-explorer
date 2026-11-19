using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.UserPaths;
using SafariExplorer.Infrastructure.JobData;

namespace SafariExplorer.Infrastructure.AzureStorage;

/// <summary>
/// One table row per saved path (ADR-0004): the partition is the trip, the row key the path id. The stops are a JSON
/// string, because a table column can't hold a list. Ids must be slugs, which are valid row keys.
/// </summary>
public sealed class TableUserPathRepository(TableClient table) : IUserPathRepository
{
    /// <summary>Every path belongs to one trip for now; a user or trip id would go here later.</summary>
    public const string Trip = "trip";

    private const int FormatVersion = 1;

    private volatile bool _tableReady;

    public async Task<IReadOnlyList<UserPath>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        var paths = new List<UserPath>();
        var rows = table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {Trip}"),
            cancellationToken: cancellationToken);
        await foreach (var row in rows)
        {
            try
            {
                paths.Add(ToPath(row));
            }
            catch (Exception e) when (e is InvalidDataException or JsonException)
            {
                // A hand-edited row that no longer parses hides that one path, not the whole trip.
            }
        }

        return paths;
    }

    public async Task SaveAsync(UserPath path, CancellationToken cancellationToken)
    {
        if (!JobFiles.Slug().IsMatch(path.Id))
        {
            throw new ArgumentException($"'{path.Id}' is not a valid path id", nameof(path));
        }

        await EnsureTableAsync(cancellationToken);
        await table.UpsertEntityAsync(ToRow(path), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (!JobFiles.Slug().IsMatch(id))
        {
            return false;
        }

        await EnsureTableAsync(cancellationToken);
        try
        {
            var response = await table.DeleteEntityAsync(Trip, id, ETag.All, cancellationToken);
            return response.Status != 404;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return false;
        }
    }

    /// <summary>Creates the table on first use, so a new account or Azurite needs no setup step.</summary>
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (!_tableReady)
        {
            await table.CreateIfNotExistsAsync(cancellationToken);
            _tableReady = true;
        }
    }

    internal static TableEntity ToRow(UserPath p) => new(Trip, p.Id)
    {
        ["Version"] = FormatVersion,
        ["DayNumber"] = p.DayNumber,
        ["Origin"] = p.Origin,
        ["Destination"] = p.Destination,
        ["Stops"] = JsonSerializer.Serialize(p.Stops.Select(s => new StopRow(s.Latitude, s.Longitude)), JobFiles.Json),
        ["SurfaceType"] = p.SurfaceType,
        ["DistanceKm"] = p.DistanceKm,
        ["DurationMin"] = p.DurationMin,
        ["CreatedAt"] = p.CreatedAt,
    };

    internal static UserPath ToPath(TableEntity row)
    {
        var stops = JsonSerializer.Deserialize<List<StopRow>>(Text(row, "Stops"), JobFiles.Json)
            ?? throw new InvalidDataException("Stops is null");
        return new UserPath(
            row.RowKey,
            Value(row.GetInt32("DayNumber"), "DayNumber"),
            Text(row, "Origin"),
            Text(row, "Destination"),
            stops.Select(s => new GeoPoint(s.Lng, s.Lat)).ToList(),
            Text(row, "SurfaceType"),
            Value(row.GetDouble("DistanceKm"), "DistanceKm"),
            Value(row.GetInt32("DurationMin"), "DurationMin"),
            Value(row.GetDateTimeOffset("CreatedAt"), "CreatedAt"));
    }

    private static string Text(TableEntity row, string column) =>
        row.GetString(column) ?? throw new InvalidDataException($"{column} is missing");

    private static T Value<T>(T? value, string column) where T : struct =>
        value ?? throw new InvalidDataException($"{column} is missing");

    private sealed record StopRow(double Lat, double Lng);
}
