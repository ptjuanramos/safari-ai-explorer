using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.UserPaths;

namespace SafariExplorer.Application.UserPaths;

public sealed class AddUserPath(IUserPathRepository repository, TimeProvider clock)
{
    /// <summary>The API has no login, so this caps how much anyone can write to the volume.</summary>
    public const int MaxPaths = 200;

    /// <summary>The saved path with its new id, or null when <see cref="MaxPaths"/> are already saved.</summary>
    /// <remarks>Call <see cref="NewUserPath.Validate"/> first.</remarks>
    public async Task<UserPath?> ExecuteAsync(NewUserPath draft, CancellationToken cancellationToken = default)
    {
        var path = new UserPath(
            Id: Guid.NewGuid().ToString("N"),
            RouteNumber: draft.RouteNumber,
            Origin: draft.Origin!.Trim(),
            Destination: draft.Destination!.Trim(),
            Stops: draft.Stops!,
            SurfaceType: draft.SurfaceType!,
            DistanceKm: draft.DistanceKm,
            DurationMin: draft.DurationMin,
            CreatedAt: clock.GetUtcNow());
            
        await repository.SaveAsync(path, cancellationToken);

        return path;
    }
}
