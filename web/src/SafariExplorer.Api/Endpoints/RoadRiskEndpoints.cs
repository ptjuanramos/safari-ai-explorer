using Microsoft.Extensions.Options;
using SafariExplorer.Api.Contracts;
using SafariExplorer.Application.RoadRisk;

namespace SafariExplorer.Api.Endpoints;

public static class RoadRiskEndpoints
{
    public static IEndpointRouteBuilder MapRoadRiskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/road-risk").WithTags("Road risk");

        group.MapGet("/places", async (ListRoadRiskPlaces useCase, CancellationToken ct) =>
            Results.Ok((await useCase.ExecuteAsync(ct)).Select(RoadRiskPlaceDto.From)));

        group.MapGet("/dates", async (string? place, ListRoadRiskDates useCase, IOptions<RoadRiskOptions> options,
            CancellationToken ct) =>
        {
            var resolved = place ?? options.Value.DefaultPlace;
            return Results.Ok(new RoadRiskDatesDto(resolved, await useCase.ExecuteAsync(resolved, ct)));
        });

        group.MapGet("/{date}", async (DateOnly date, string? place, GetRoadRiskByDate useCase,
                IOptions<RoadRiskOptions> options, CancellationToken ct) =>
            await useCase.ExecuteAsync(place ?? options.Value.DefaultPlace, date, ct) is { } snapshot
                ? Results.Ok(RoadRiskFeatureCollectionDto.From(snapshot))
                : Results.NotFound());

        // Without ?place, every park with data: a trip usually crosses several.
        group.MapPost("/routes", async (RouteRiskRequestDto request, string? place, GetRouteRisk useCase,
            CancellationToken ct) =>
        {
            if (request.Validate() is { } problem)
            {
                return Results.BadRequest(new { error = problem });
            }

            return await useCase.ExecuteAsync(place, request.Date, request.ToRoutes(), ct)
                is { } report
                ? Results.Ok(RouteRiskReportDto.From(report))
                : Results.NotFound();
        });

        return app;
    }
}
