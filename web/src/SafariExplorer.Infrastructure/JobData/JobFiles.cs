using System.Text.Json;
using System.Text.RegularExpressions;

namespace SafariExplorer.Infrastructure.JobData;

/// <summary>The rules every job's files share (ADR-0002): a run is a date folder with a manifest, and
/// <c>latest.json</c> names the newest run.</summary>
internal static partial class JobFiles
{
    public const string Manifest = "manifest.json";
    public const string Latest = "latest.json";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex Slug();

    public static string RunId(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static string ManifestKey(string jobFolder, DateOnly date) => $"{jobFolder}/{RunId(date)}/{Manifest}";

    /// <summary>The key's segments; throws when one is empty, <c>.</c> or <c>..</c>, so no key leaves the store's root.</summary>
    public static string[] Segments(string key)
    {
        var segments = key.Split('/');
        if (segments.Any(s => s is "" or "." or ".." || s.Contains('\\')))
        {
            throw new ArgumentException($"'{key}' is not a valid job file key", nameof(key));
        }

        return segments;
    }

    /// <summary>The run <c>latest.json</c> points at, or null when it's missing or that run has no manifest.</summary>
    public static async Task<DateOnly?> ReadLatestAsync(IJobStore store, string jobFolder, CancellationToken cancellationToken)
    {
        var latest = await store.ReadJsonAsync<LatestFile>($"{jobFolder}/{Latest}", cancellationToken);
        if (latest is null || !DateOnly.TryParseExact(latest.Run, "yyyy-MM-dd", out var date))
        {
            return null;
        }

        return await store.ExistsAsync(ManifestKey(jobFolder, date), cancellationToken) ? date : null;
    }

    /// <summary>The dates of the runs with a manifest, oldest first, from one listing (not one check per date).</summary>
    public static async Task<IReadOnlyList<DateOnly>> ListRunsAsync(IJobStore store, string jobFolder,
        CancellationToken cancellationToken)
    {
        var dates = new List<DateOnly>();
        await foreach (var key in store.ListKeysAsync(jobFolder, cancellationToken))
        {
            if (key[(jobFolder.Length + 1)..].Split('/') is [var name, Manifest]
                && DateOnly.TryParseExact(name, "yyyy-MM-dd", out var date))
            {
                dates.Add(date);
            }
        }

        return dates.Order().ToList();
    }
}

internal sealed record LatestFile(string Run, string Manifest, DateTimeOffset UpdatedAt);
