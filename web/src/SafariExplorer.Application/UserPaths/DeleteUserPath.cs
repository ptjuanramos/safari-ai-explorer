using SafariExplorer.Application.Abstractions;

namespace SafariExplorer.Application.UserPaths;

public sealed class DeleteUserPath(IUserPathRepository repository)
{
    /// <summary>False when there was no path with that id.</summary>
    public Task<bool> ExecuteAsync(string id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);
}
