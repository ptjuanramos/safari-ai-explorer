using SafariExplorer.Api.Contracts;
using SafariExplorer.Application.Directions;

namespace SafariExplorer.Api.Endpoints;

public static class DirectionsEndpoints
{
    public static IEndpointRouteBuilder MapDirectionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/directions", async (DirectionsRequestDto request, GetDrivingRoute useCase,
            CancellationToken ct) =>
        {
            if (!useCase.IsAvailable)
            {
                return Results.Problem("Directions are not configured: set GOOGLE_MAPS_API_KEY on the API.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var via = request.Intermediates ?? [];
            if (via.Count > DirectionsRequestDto.MaxIntermediates)
            {
                return Results.BadRequest(new { error = $"at most {DirectionsRequestDto.MaxIntermediates} intermediates" });
            }

            var route = await useCase.ExecuteAsync(request.Origin.ToPoint(), request.Destination.ToPoint(),
                via.Select(p => p.ToPoint()).ToList(), ct);
            return route is null ? Results.NotFound() : Results.Ok(DrivingRouteDto.From(route));
        }).WithTags("Directions");

        return app;
    }
}
