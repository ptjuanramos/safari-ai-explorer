namespace SafariExplorer.Infrastructure.Google;

/// <summary>Server-side Google Maps settings, from <c>GoogleMaps:ApiKey</c> or the <c>GOOGLE_MAPS_API_KEY</c> env var.</summary>
public sealed class GoogleMapsOptions
{
    public const string Section = "GoogleMaps";

    /// <summary>A server key with the Routes API and Roads API enabled. Empty disables directions.</summary>
    public string ApiKey { get; set; } = "";

    public string RoutesUrl { get; set; } = "https://routes.googleapis.com/directions/v2:computeRoutes";

    public string RoadsUrl { get; set; } = "https://roads.googleapis.com/v1/nearestRoads";
}
