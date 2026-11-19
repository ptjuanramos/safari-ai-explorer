using SafariExplorer.Api.Contracts;
using SafariExplorer.Application.UserPaths;

namespace SafariExplorer.Api.Endpoints;

public static class UserPathEndpoints
{
    public static IEndpointRouteBuilder MapUserPathEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user-paths").WithTags("User paths");

        group.MapGet("/", async (ListUserPaths useCase, CancellationToken ct) =>
            Results.Ok((await useCase.ExecuteAsync(ct)).Select(UserPathDto.From)));

        group.MapPost("/", async (NewUserPathDto request, AddUserPath useCase, CancellationToken ct) =>
        {
            var draft = request.ToDraft();
            if (draft.Validate() is { } problem)
            {
                return Results.BadRequest(new { error = problem });
            }

            return await useCase.ExecuteAsync(draft, ct) is { } path
                ? Results.Created($"/api/user-paths/{path.Id}", UserPathDto.From(path))
                : Results.Problem($"At most {AddUserPath.MaxPaths} paths can be saved: delete some first.",
                    statusCode: StatusCodes.Status409Conflict);
        });

        group.MapDelete("/{id}", async (string id, DeleteUserPath useCase, CancellationToken ct) =>
            await useCase.ExecuteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
