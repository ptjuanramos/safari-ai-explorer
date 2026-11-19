using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Infrastructure.Google;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class GoogleRoutesClientTests
{
    private static readonly GeoPoint Origin = new(36.0, -3.7);
    private static readonly GeoPoint Destination = new(35.8, -3.4);

    [Fact]
    public async Task Snaps_the_points_then_returns_the_route()
    {
        var handler = new FakeGoogle(routeBodies: [RouteJson]);
        var client = Client(handler);

        var route = await client.GetRouteAsync(Origin, Destination, [], CancellationToken.None);

        Assert.NotNull(route);
        Assert.Equal(("abc", 86.0, 160), (route.EncodedPolyline, route.DistanceKm, route.DurationMin));
        Assert.Equal(2, handler.RoadsCalls);
        Assert.Contains("\"latitude\":-1.5", handler.RouteRequests[0]);
        Assert.All(handler.RouteKeys, k => Assert.Equal("test-key", k));
    }

    [Fact]
    public async Task Retries_without_stops_when_no_route_is_found()
    {
        var handler = new FakeGoogle(routeBodies: ["{}", RouteJson]);

        var route = await Client(handler).GetRouteAsync(Origin, Destination, [new GeoPoint(35.9, -3.5)],
            CancellationToken.None);

        Assert.NotNull(route);
        Assert.Contains("intermediates", handler.RouteRequests[0]);
        Assert.DoesNotContain("intermediates", handler.RouteRequests[1]);
    }

    [Fact]
    public async Task No_route_anywhere_is_null()
    {
        var route = await Client(new FakeGoogle(routeBodies: ["{}", "{}"])).GetRouteAsync(Origin, Destination, [],
            CancellationToken.None);

        Assert.Null(route);
    }

    [Fact]
    public void Without_a_key_it_is_not_configured()
    {
        var client = new GoogleRoutesClient(new HttpClient(new FakeGoogle([])), Options.Create(new GoogleMapsOptions()));

        Assert.False(client.IsConfigured);
    }

    private const string RouteJson =
        """{"routes":[{"polyline":{"encodedPolyline":"abc"},"distanceMeters":86000,"duration":"9600s"}]}""";

    private static GoogleRoutesClient Client(FakeGoogle handler) =>
        new(new HttpClient(handler), Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

    /// <summary>Answers Roads calls with a snapped point at (-1.5, 35.5), and Routes calls with the given bodies in turn.</summary>
    private sealed class FakeGoogle(string[] routeBodies) : HttpMessageHandler
    {
        private int _routeCall;

        public int RoadsCalls { get; private set; }

        public List<string> RouteRequests { get; } = [];

        public List<string?> RouteKeys { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.StartsWith("roads", StringComparison.Ordinal))
            {
                RoadsCalls++;
                return Json("""{"snappedPoints":[{"location":{"latitude":-1.5,"longitude":35.5}}]}""");
            }

            RouteRequests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            RouteKeys.Add(request.Headers.GetValues("X-Goog-Api-Key").SingleOrDefault());
            return Json(routeBodies[Math.Min(_routeCall++, routeBodies.Length - 1)]);
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
