using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.JobData;
using SafariExplorer.Tests;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class RoadRiskRepositoryTests : IDisposable
{
    private readonly TestDataFolder _data = new();

    private RoadRiskRepository Repository => new(new LocalJobStore(_data.Root));

    public void Dispose() => _data.Dispose();

    [Fact]
    public async Task Places_are_the_folders_with_a_latest_run()
    {
        _data.WithRoadRun("2026-11-15").WithLatest("road-risk/test-park", "2026-11-15")
            .WithRoadRun("2026-11-14", place: "another-park").WithLatest("road-risk/another-park", "2026-11-14")
            .WithRoadRun("2026-11-15", place: "no-latest-yet")
            .WithRoadRun("2026-11-15", place: "Not_A_Slug").WithLatest("road-risk/Not_A_Slug", "2026-11-15");

        var places = await Repository.ListPlacesAsync(CancellationToken.None);

        Assert.Equal(["another-park", "test-park"], places);
    }

    [Fact]
    public async Task No_road_risk_folder_means_no_places()
    {
        Assert.Empty(await Repository.ListPlacesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Latest_follows_latest_json()
    {
        _data.WithRoadRun("2026-11-14").WithRoadRun("2026-11-15").WithLatest("road-risk/test-park", "2026-11-14");

        var latest = await Repository.GetLatestDateAsync(TestDataFolder.Place, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 14), latest);
    }

    [Fact]
    public async Task Run_is_read_into_domain_models()
    {
        _data.WithRoadRun("2026-11-15");

        var snapshot = await Repository.GetAsync(TestDataFolder.Place, new DateOnly(2026, 11, 15), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal("IMERG", snapshot.Run.Provider);
        Assert.False(snapshot.Run.Complete);
        Assert.Equal(8, snapshot.Run.GranuleCoverage[new DateOnly(2026, 11, 15)]);
        Assert.Equal(2, snapshot.Segments.Count);
        var track = snapshot.Segments[0];
        Assert.Equal(("1-2-0", "track", 0.45, 12.5), (track.Id, track.RoadType, track.SurfaceRisk, track.RainIndexMm));
        Assert.Equal(36.1, track.Line[1].Longitude);
        Assert.Equal(-4.05, track.Line[1].Latitude);
        Assert.Null(snapshot.Segments[1].RainIndexMm);
    }

    [Fact]
    public async Task Missing_run_is_null()
    {
        var snapshot = await Repository.GetAsync(TestDataFolder.Place, new DateOnly(2000, 1, 1), CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Half_written_run_is_invisible()
    {
        _data.WithRoadRun("2026-11-15").WithRoadRun("2026-11-16", withManifest: false)
            .WithLatest("road-risk/test-park", "2026-11-16");

        Assert.Equal([new DateOnly(2026, 11, 15)], await Repository.ListDatesAsync(TestDataFolder.Place, CancellationToken.None));
        Assert.Null(await Repository.GetAsync(TestDataFolder.Place, new DateOnly(2026, 11, 16), CancellationToken.None));
        Assert.Null(await Repository.GetLatestDateAsync(TestDataFolder.Place, CancellationToken.None));
    }

    [Theory]
    [InlineData("../road-risk/test-park")]
    [InlineData("..")]
    [InlineData("Test-Park")]
    [InlineData("test park")]
    public async Task Place_outside_the_slug_pattern_is_rejected(string place)
    {
        _data.WithRoadRun("2026-11-15").WithLatest("road-risk/test-park", "2026-11-15");

        Assert.Empty(await Repository.ListDatesAsync(place, CancellationToken.None));
        Assert.Null(await Repository.GetLatestDateAsync(place, CancellationToken.None));
        Assert.Null(await Repository.GetAsync(place, new DateOnly(2026, 11, 15), CancellationToken.None));
    }

    [Fact]
    public async Task Dates_are_sorted_oldest_first()
    {
        _data.WithRoadRun("2026-11-15").WithRoadRun("2024-04-30");

        var dates = await Repository.ListDatesAsync(TestDataFolder.Place, CancellationToken.None);

        Assert.Equal([new DateOnly(2024, 4, 30), new DateOnly(2026, 11, 15)], dates);
    }
}
