using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.Directions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Infrastructure.Google;

/// <summary>
/// Driving directions from the Google Routes API, with every point first snapped to the nearest road (Roads API).
/// Points inside national parks often sit off any road, and then Routes finds no route at all.
/// </summary>
public sealed class GoogleRoutesClient(HttpClient http, IOptions<GoogleMapsOptions> options) : IDirectionsService
{
    private const string FieldMask = "routes.polyline.encodedPolyline,routes.distanceMeters,routes.duration";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private GoogleMapsOptions Settings => options.Value;

    public bool IsConfigured => Settings.ApiKey.Length > 0;

    public async Task<DrivingRoute?> GetRouteAsync(GeoPoint origin, GeoPoint destination, IReadOnlyList<GeoPoint> via,
        CancellationToken cancellationToken)
    {
        var snapped = await Task.WhenAll(new[] { origin, destination }.Concat(via)
            .Select(p => NearestRoadAsync(p, cancellationToken)));
        var (snappedOrigin, snappedDestination, snappedVia) = (snapped[0], snapped[1], snapped[2..]);

        // Try with the stops first, then without them, then with the points as given.
        return await ComputeAsync(snappedOrigin, snappedDestination, snappedVia, cancellationToken)
            ?? (snappedVia.Length > 0 ? await ComputeAsync(snappedOrigin, snappedDestination, [], cancellationToken) : null)
            ?? await ComputeAsync(origin, destination, [], cancellationToken);
    }

    private async Task<GeoPoint> NearestRoadAsync(GeoPoint point, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{Settings.RoadsUrl}?points={point.Latitude},{point.Longitude}&key={Uri.EscapeDataString(Settings.ApiKey)}");
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return point;
            }

            var body = await response.Content.ReadFromJsonAsync<NearestRoadsResponse>(Json, cancellationToken);
            return body?.SnappedPoints?.FirstOrDefault()?.Location is { } loc
                ? new GeoPoint(loc.Longitude, loc.Latitude)
                : point;
        }
        catch (HttpRequestException)
        {
            return point;
        }
    }

    private async Task<DrivingRoute?> ComputeAsync(GeoPoint origin, GeoPoint destination, GeoPoint[] via,
        CancellationToken cancellationToken)
    {
        var request = new ComputeRoutesRequest(ToWaypoint(origin), ToWaypoint(destination),
            via.Length > 0 ? via.Select(ToWaypoint).ToList() : null);
        using var message = new HttpRequestMessage(HttpMethod.Post, Settings.RoutesUrl)
        {
            Content = JsonContent.Create(request, options: Json),
        };
        message.Headers.Add("X-Goog-Api-Key", Settings.ApiKey);
        message.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<ComputeRoutesResponse>(Json, cancellationToken);
        if (body?.Routes?.FirstOrDefault() is not { Polyline.EncodedPolyline: { Length: > 0 } polyline } route)
        {
            return null;
        }

        return new DrivingRoute(polyline, route.DistanceMeters / 1000.0, (int)Math.Round(Seconds(route.Duration) / 60));
    }

    /// <summary>Seconds in a Routes API duration such as "1234s".</summary>
    private static double Seconds(string? duration) =>
        double.TryParse(duration?.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0;

    private static Waypoint ToWaypoint(GeoPoint p) => new(new Location(new LatLng(p.Latitude, p.Longitude)));

    private sealed record ComputeRoutesRequest(Waypoint Origin, Waypoint Destination, List<Waypoint>? Intermediates)
    {
        public string TravelMode => "DRIVE";

        public string PolylineEncoding => "ENCODED_POLYLINE";
    }

    private sealed record Waypoint(Location Location);

    private sealed record Location(LatLng LatLng);

    private sealed record LatLng(double Latitude, double Longitude);

    private sealed record ComputeRoutesResponse(List<Route>? Routes);

    private sealed record Route(Polyline? Polyline, double DistanceMeters, string? Duration);

    private sealed record Polyline(string? EncodedPolyline);

    private sealed record NearestRoadsResponse(List<SnappedPoint>? SnappedPoints);

    private sealed record SnappedPoint(LatLng? Location);
}
