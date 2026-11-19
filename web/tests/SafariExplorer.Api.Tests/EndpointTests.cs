using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SafariExplorer.Tests;

namespace SafariExplorer.Api.Tests;

public sealed class EndpointTests : IDisposable
{
    private readonly TestDataFolder _data = new TestDataFolder()
        .WithRoadRun("2024-04-30", place: "tarangire-national-park-tanzania")
        .WithRoadRun("2026-11-15", place: "tarangire-national-park-tanzania")
        .WithLatest("road-risk/tarangire-national-park-tanzania", "2026-11-15")
        .WithSightingsRun("2026-11-15")
        .WithLatest("sightings", "2026-11-15");

    private readonly WebApplicationFactory<Program> _factory;

    public EndpointTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseSetting("DATA_DIR", _data.Root)
                .UseSetting("GOOGLE_MAPS_API_KEY", "")
                .UseSetting("GoogleMaps:ApiKey", ""));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _data.Dispose();
    }

    [Fact]
    public async Task Road_risk_by_date_is_a_geojson_feature_collection()
    {
        using var json = await GetJson("/api/road-risk/2026-11-15");
        var root = json.RootElement;

        Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());
        Assert.Equal("2026-11-15", root.GetProperty("run").GetProperty("date").GetString());
        var feature = root.GetProperty("features")[0];
        Assert.Equal("Feature", feature.GetProperty("type").GetString());
        Assert.Equal("LineString", feature.GetProperty("geometry").GetProperty("type").GetString());
        Assert.Equal(36.0, feature.GetProperty("geometry").GetProperty("coordinates")[0][0].GetDouble());
        Assert.Equal(0.45, feature.GetProperty("properties").GetProperty("surfaceRisk").GetDouble());
    }

    [Fact]
    public async Task Road_risk_places_list_each_park_with_its_latest_run()
    {
        using var places = await GetJson("/api/road-risk/places");
        var place = Assert.Single(places.RootElement.EnumerateArray());
        Assert.Equal("tarangire-national-park-tanzania", place.GetProperty("place").GetString());
        Assert.Equal("2026-11-15", place.GetProperty("latestDate").GetString());
    }

    [Fact]
    public async Task Road_risk_dates_and_by_date()
    {
        using var dates = await GetJson("/api/road-risk/dates");
        Assert.Equal(["2024-04-30", "2026-11-15"],
            dates.RootElement.GetProperty("dates").EnumerateArray().Select(d => d.GetString()));

        using var past = await GetJson("/api/road-risk/2024-04-30");
        Assert.Equal("2024-04-30", past.RootElement.GetProperty("run").GetProperty("date").GetString());
    }

    [Theory]
    [InlineData("/api/road-risk/2000-01-01")]
    [InlineData("/api/road-risk/2026-11-15?place=..%2F..%2Fsightings")]
    [InlineData("/api/road-risk/2026-11-15?place=other-park")]
    [InlineData("/api/sightings/zebra/5000/0/0")]
    [InlineData("/api/sightings/lion/3000/0/0")]
    [InlineData("/api/sightings/lion/5000/9/9")]
    [InlineData("/api/sightings/lion/5000")]
    [InlineData("/api/unknown")]
    public async Task Missing_or_invalid_requests_are_not_found(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_date_is_a_bad_request()
    {
        var response = await _factory.CreateClient().GetAsync("/api/road-risk/not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sightings_latest_and_tile()
    {
        using var run = await GetJson("/api/sightings/latest");
        Assert.Equal(2, run.RootElement.GetProperty("species").GetArrayLength());
        var layer = run.RootElement.GetProperty("layers")[0];
        Assert.Equal(5000, layer.GetProperty("sigmaM").GetInt32());
        Assert.Equal(1900, layer.GetProperty("speciesRecords").GetProperty("lion").GetInt32());
        Assert.Equal(0, layer.GetProperty("tiles")[0].GetProperty("tx").GetInt32());

        using var tile = await GetJson("/api/sightings/lion/5000/0/0");
        var share = tile.RootElement.GetProperty("share");
        Assert.Equal(0.12, share[0].GetDouble());
        Assert.Equal(JsonValueKind.Null, share[1].ValueKind);
    }

    [Fact]
    public async Task Route_risk_scores_each_route_from_the_newest_run()
    {
        var body = new
        {
            date = "2026-12-01",
            routes = new object[]
            {
                new { id = "day-1", path = new[] { new { lat = -4.0, lng = 36.0 }, new { lat = -4.05, lng = 36.1 },
                    new { lat = -4.0, lng = 36.2 } } },
                new { id = "outside", path = new[] { new { lat = -2.0, lng = 35.0 }, new { lat = -2.1, lng = 35.1 } } },
            },
        };

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/road-risk/routes", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        var run = Assert.Single(root.GetProperty("runs").EnumerateArray());
        Assert.Equal("2026-11-15", run.GetProperty("date").GetString());
        Assert.Equal("tarangire-national-park-tanzania", run.GetProperty("place").GetString());
        Assert.False(run.GetProperty("complete").GetBoolean());
        Assert.Equal("2026-12-01", root.GetProperty("requestedDate").GetString());
        var day1 = root.GetProperty("routes")[0];
        Assert.Equal(2, day1.GetProperty("matchedEdges").GetInt32());
        Assert.Equal(0.45, day1.GetProperty("maxSurfaceRisk").GetDouble());
        Assert.Equal(0.275, day1.GetProperty("meanSurfaceRisk").GetDouble(), tolerance: 0.005);
        Assert.Equal("tarangire-national-park-tanzania", day1.GetProperty("places")[0].GetString());
        var outside = root.GetProperty("routes")[1];
        Assert.Equal(JsonValueKind.Null, outside.GetProperty("meanSurfaceRisk").ValueKind);
        Assert.Equal(0, outside.GetProperty("coverage").GetDouble());
    }

    [Fact]
    public async Task Route_risk_before_every_run_is_not_found_and_empty_routes_are_bad()
    {
        var client = _factory.CreateClient();
        var path = new[] { new { lat = -4.0, lng = 36.0 }, new { lat = -4.05, lng = 36.1 } };

        var early = await client.PostAsJsonAsync("/api/road-risk/routes",
            new { date = "2020-01-01", routes = new[] { new { id = "a", path } } });
        var empty = await client.PostAsJsonAsync("/api/road-risk/routes", new { date = "2026-11-15", routes = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, early.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Directions_without_a_key_are_unavailable()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/directions",
            new { origin = new { lat = -3.7, lng = 36.0 }, destination = new { lat = -3.4, lng = 35.8 } });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task User_paths_are_saved_listed_and_deleted()
    {
        var client = _factory.CreateClient();
        var body = new
        {
            routeNumber = 1,
            origin = "Tarangire Gate",
            destination = "Silale Swamp",
            stops = new[] { new { lat = -3.7167, lng = 36.0167 }, new { lat = -4.0, lng = 36.08 } },
            surfaceType = "track",
            distanceKm = 42.5,
            durationMin = 95,
        };

        using var empty = await GetJson("/api/user-paths");
        Assert.Equal(0, empty.RootElement.GetArrayLength());

        var created = await client.PostAsJsonAsync("/api/user-paths", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var path = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = path.RootElement.GetProperty("id").GetString();
        Assert.Equal($"/api/user-paths/{id}", created.Headers.Location?.OriginalString);
        Assert.True(File.Exists(Path.Combine(_data.Root, "user-paths", $"{id}.json")));

        using var listed = await GetJson("/api/user-paths");
        var first = listed.RootElement[0];
        Assert.Equal(id, first.GetProperty("id").GetString());
        Assert.Equal(-4.0, first.GetProperty("stops")[1].GetProperty("lat").GetDouble());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/user-paths/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/user-paths/{id}")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_user_path_is_a_bad_request()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/user-paths", new
        {
            routeNumber = 1,
            origin = "A",
            destination = "B",
            stops = new[] { new { lat = -3.7, lng = 36.0 } },
            surfaceType = "track",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("stops", await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
