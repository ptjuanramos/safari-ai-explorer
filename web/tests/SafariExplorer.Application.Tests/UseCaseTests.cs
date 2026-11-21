using SafariExplorer.Application.Abstractions;
using SafariExplorer.Application.RoadRisk;
using SafariExplorer.Application.Sightings;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Application.Tests;

public sealed class UseCaseTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);

    [Theory]
    [InlineData("2026-12-01", "2026-11-15")]
    [InlineData("2026-11-15", "2026-11-15")]
    [InlineData("2026-11-14", "2026-11-10")]
    [InlineData("2026-11-10", "2026-11-10")]
    public async Task Route_risk_uses_the_newest_run_on_or_before_the_date(string requested, string expected)
    {
        var repository = new FakeRoadRiskRepository { Latest = Today };
        repository.Snapshots[Today] = Snapshot(Today);
        repository.Snapshots[new DateOnly(2026, 11, 10)] = Snapshot(new DateOnly(2026, 11, 10));
        var matcher = new FakeRouteMatcher();

        var report = await new GetRouteRisk(repository, matcher)
            .ExecuteAsync("park", DateOnly.Parse(requested), [Route("day-1")]);

        Assert.NotNull(report);
        Assert.Equal(DateOnly.Parse(expected), Assert.Single(report.Runs).Date);
        Assert.Equal(DateOnly.Parse(requested), report.RequestedDate);
        Assert.Equal(["day-1"], report.Routes.Select(r => r.Id));
        Assert.Single(matcher.Calls);
        Assert.Equal(["park"], report.Routes[0].Places);
    }

    [Fact]
    public async Task Route_risk_without_a_place_merges_every_park_and_says_which_parks_each_route_touches()
    {
        var repository = new MultiPlaceRepository();
        repository.Add("park-a", Today, Segment("a-1"));
        // Park B is a day behind and shares edge a-1 with park A (a border road).
        repository.Add("park-b", Today.AddDays(-1), Segment("a-1"), Segment("b-1"));
        var matcher = new PrefixMatcher();

        var report = await new GetRouteRisk(repository, matcher).ExecuteAsync(null, Today, [Route("a"), Route("b"), Route("c")]);

        Assert.NotNull(report);
        Assert.Equal([("park-a", Today), ("park-b", Today.AddDays(-1))], report.Runs.Select(r => (r.Place, r.Date)));
        Assert.Equal(["a-1", "b-1"], matcher.Networks[0].Select(s => s.Id));
        Assert.Equal(["park-a", "park-b"], report.Routes.Single(r => r.Id == "a").Places);
        Assert.Equal(["park-b"], report.Routes.Single(r => r.Id == "b").Places);
        Assert.Empty(report.Routes.Single(r => r.Id == "c").Places);
        Assert.Equal(1, report.Routes.Single(r => r.Id == "a").MatchedEdges);
        // One match over every park's roads, not one more per park.
        Assert.Single(matcher.Networks);
    }

    [Fact]
    public async Task Road_risk_places_list_each_park_with_its_latest_run()
    {
        var repository = new MultiPlaceRepository();
        repository.Add("park-a", Today, Segment("a-1"));
        repository.Add("park-b", Today.AddDays(-1), Segment("b-1"));

        var places = await new ListRoadRiskPlaces(repository).ExecuteAsync();

        Assert.Equal([new RoadRiskPlace("park-a", Today), new RoadRiskPlace("park-b", Today.AddDays(-1))], places);
    }

    [Fact]
    public async Task Route_risk_without_a_recent_enough_run_is_null()
    {
        var repository = new FakeRoadRiskRepository { Latest = Today };
        repository.Snapshots[Today] = Snapshot(Today);
        var matcher = new FakeRouteMatcher();

        repository.Snapshots[new DateOnly(2024, 4, 30)] = Snapshot(new DateOnly(2024, 4, 30));

        Assert.Null(await new GetRouteRisk(repository, matcher).ExecuteAsync("park", Today.AddDays(-1), [Route("a")]));
        Assert.Null(await new GetRouteRisk(repository, matcher)
            .ExecuteAsync("park", new DateOnly(2024, 5, 8), [Route("a")]));
        Assert.Null(await new GetRouteRisk(new FakeRoadRiskRepository(), matcher).ExecuteAsync("park", Today, [Route("a")]));
        Assert.Empty(matcher.Calls);
    }

    [Fact]
    public async Task Road_risk_by_date_and_dates_pass_through()
    {
        var repository = new FakeRoadRiskRepository();
        repository.Snapshots[Today] = Snapshot(Today);

        Assert.NotNull(await new GetRoadRiskByDate(repository).ExecuteAsync("park", Today));
        Assert.Null(await new GetRoadRiskByDate(repository).ExecuteAsync("park", Today.AddDays(-1)));
        Assert.Equal([Today], await new ListRoadRiskDates(repository).ExecuteAsync("park"));
    }

    [Fact]
    public async Task Sightings_tile_comes_from_the_latest_run()
    {
        var repository = new FakeSightingsRepository { Run = Run(Today) };

        var tile = await new GetSightingsTile(repository).ExecuteAsync("lion", 5000, 1, 2);

        Assert.Equal(("lion", 1, 2), (tile?.Species, tile?.Tx, tile?.Ty));
        Assert.Equal([(Today, "lion", 5000, 1, 2)], repository.TileRequests);
    }

    [Theory]
    [InlineData("zebra", 5000, 1, 2)]
    [InlineData("lion", 1234, 1, 2)]
    [InlineData("lion", 5000, 0, 0)]
    public async Task Sightings_tile_outside_the_run_is_never_requested(string species, int sigmaM, int tx, int ty)
    {
        var repository = new FakeSightingsRepository { Run = Run(Today) };

        Assert.Null(await new GetSightingsTile(repository).ExecuteAsync(species, sigmaM, tx, ty));
        Assert.Empty(repository.TileRequests);
    }

    [Fact]
    public async Task Sightings_without_a_run_are_null()
    {
        var repository = new FakeSightingsRepository();

        Assert.Null(await new GetLatestSightingsRun(repository).ExecuteAsync());
        Assert.Null(await new GetSightingsTile(repository).ExecuteAsync("lion", 5000, 1, 2));
    }

    private static RoadRiskSnapshot Snapshot(DateOnly date) => new(
        new RoadRiskRun("park", date, "IMERG", 5, 1, false, 48, new Dictionary<DateOnly, int>(), "note",
            DateTimeOffset.UnixEpoch),
        [new RoadSegmentRisk("1-2-0", "track", 100, 0.5, 10, 0.45, [new GeoPoint(36, -4), new GeoPoint(36.1, -4)])]);

    private static RoadSegmentRisk Segment(string id) =>
        new(id, "track", 100, 0.5, 10, 0.45, [new GeoPoint(36, -4), new GeoPoint(36.1, -4)]);

    private static TripRoute Route(string id) => new(id, [new GeoPoint(36, -4), new GeoPoint(36.1, -4)]);

    private static SightingsRun Run(DateOnly date) => new(
        date, 2017, 2026, 100,
        [
            new SightingsLayer(5000, 2500, 256, null, 100, new Dictionary<string, int> { ["lion"] = 10 },
                new Dictionary<string, double> { ["lion"] = 0.1 }, new LayerGrid(0, 0, 300, 300),
                [new TileRef(1, 2, [34, -3.6, 34.5, -3.1])]),
        ],
        [new SpeciesSummary("lion", 5219404, 10, 0.1, 0.25)], "meaning", DateTimeOffset.UnixEpoch);

    private sealed class FakeRoadRiskRepository : IRoadRiskRepository
    {
        public DateOnly? Latest { get; init; }

        public Dictionary<DateOnly, RoadRiskSnapshot> Snapshots { get; } = [];

        public List<(string Place, DateOnly Date)> Requests { get; } = [];

        public Task<IReadOnlyList<string>> ListPlacesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["park"]);

        public Task<IReadOnlyList<DateOnly>> ListDatesAsync(string place, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DateOnly>>(Snapshots.Keys.Order().ToList());

        public Task<DateOnly?> GetLatestDateAsync(string place, CancellationToken cancellationToken) =>
            Task.FromResult(Latest);

        public Task<RoadRiskSnapshot?> GetAsync(string place, DateOnly date, CancellationToken cancellationToken)
        {
            Requests.Add((place, date));
            return Task.FromResult(Snapshots.GetValueOrDefault(date));
        }
    }

    /// <summary>Several places, each with one run (its latest).</summary>
    private sealed class MultiPlaceRepository : IRoadRiskRepository
    {
        private readonly Dictionary<string, RoadRiskSnapshot> _latest = [];

        public void Add(string place, DateOnly date, params RoadSegmentRisk[] segments) =>
            _latest[place] = new RoadRiskSnapshot(
                new RoadRiskRun(place, date, "IMERG", 5, segments.Length, true, 48, new Dictionary<DateOnly, int>(),
                    "note", DateTimeOffset.UnixEpoch),
                segments);

        public Task<IReadOnlyList<string>> ListPlacesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(_latest.Keys.Order().ToList());

        public Task<IReadOnlyList<DateOnly>> ListDatesAsync(string place, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DateOnly>>(_latest.TryGetValue(place, out var s) ? [s.Run.Date] : []);

        public Task<DateOnly?> GetLatestDateAsync(string place, CancellationToken cancellationToken) =>
            Task.FromResult(_latest.TryGetValue(place, out var s) ? s.Run.Date : (DateOnly?)null);

        public Task<RoadRiskSnapshot?> GetAsync(string place, DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(_latest.TryGetValue(place, out var s) && s.Run.Date == date ? s : null);
    }

    /// <summary>Route "a" matches the edges whose id starts with "a-", and so on.</summary>
    private sealed class PrefixMatcher : IRouteMatcher
    {
        public List<IReadOnlyList<RoadSegmentRisk>> Networks { get; } = [];

        public IReadOnlyList<RouteRisk> Match(IReadOnlyList<RoadSegmentRisk> network, IReadOnlyList<TripRoute> routes)
        {
            Networks.Add(network);
            return routes.Select(r =>
            {
                var ids = network.Where(s => s.Id.StartsWith(r.Id + "-", StringComparison.Ordinal)).Select(s => s.Id).ToList();
                return new RouteRisk(r.Id, 1000, ids.Count * 100, ids.Count, ids.Count > 0 ? 0.4 : null, null, null, [])
                {
                    MatchedEdgeIds = ids,
                };
            }).ToList();
        }
    }

    private sealed class FakeRouteMatcher : IRouteMatcher
    {
        public List<IReadOnlyList<TripRoute>> Calls { get; } = [];

        public IReadOnlyList<RouteRisk> Match(IReadOnlyList<RoadSegmentRisk> network, IReadOnlyList<TripRoute> routes)
        {
            Calls.Add(routes);
            return routes.Select(r => new RouteRisk(r.Id, 1000, 500, 1, 0.4, 0.5, 12, ["track"])).ToList();
        }
    }

    private sealed class FakeSightingsRepository : ISightingsRepository
    {
        public SightingsRun? Run { get; init; }

        public List<(DateOnly Date, string Species, int SigmaM, int Tx, int Ty)> TileRequests { get; } = [];

        public Task<SightingsRun?> GetLatestRunAsync(CancellationToken cancellationToken) => Task.FromResult(Run);

        public Task<SightingsTile?> GetTileAsync(DateOnly runDate, string species, int sigmaM, int tx, int ty,
            CancellationToken cancellationToken)
        {
            TileRequests.Add((runDate, species, sigmaM, tx, ty));
            return Task.FromResult<SightingsTile?>(new SightingsTile(
                species, "EPSG:32736", sigmaM, sigmaM / 2.0, tx, ty, 0, 0, 1, 1, [34, -3.6, 34.5, -3.1], 0.1, 10,
                [0.1], [20], [0.05], [0.15]));
        }
    }
}
