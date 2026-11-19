using SafariExplorer.Api.Contracts;
using SafariExplorer.Application.Sightings;

namespace SafariExplorer.Api.Endpoints;

public static class SightingsEndpoints
{
    public static IEndpointRouteBuilder MapSightingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sightings").WithTags("Sightings");

        group.MapGet("/latest", async (GetLatestSightingsRun useCase, CancellationToken ct) =>
            await useCase.ExecuteAsync(ct) is { } run
                ? Results.Ok(SightingsRunDto.From(run))
                : Results.NotFound());

        group.MapGet("/{species}/{sigmaM:int}/{tx:int}/{ty:int}", async (string species, int sigmaM, int tx, int ty,
                GetSightingsTile useCase, CancellationToken ct) =>
            await useCase.ExecuteAsync(species, sigmaM, tx, ty, ct) is { } tile
                ? Results.Ok(SightingsTileDto.From(tile))
                : Results.NotFound());

        return app;
    }
}
