using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.UserPaths;

namespace SafariExplorer.Application.UserPaths;

public sealed class ListUserPaths(IUserPathRepository repository)
{
    /// <summary>The saved paths in trip order: by day, then in the order they were added.</summary>
    public async Task<IReadOnlyList<UserPath>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        (await repository.ListAsync(cancellationToken))
            .OrderBy(p => p.RouteNumber)
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .ToList();
}
