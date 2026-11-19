using SafariExplorer.Domain.UserPaths;

namespace SafariExplorer.Application.Abstractions;

/// <summary>The paths the user saved. Unlike the job outputs, the API writes these.</summary>
public interface IUserPathRepository
{
    /// <summary>Every saved path, in no particular order.</summary>
    Task<IReadOnlyList<UserPath>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates the path, or replaces the one with the same id.</summary>
    Task SaveAsync(UserPath path, CancellationToken cancellationToken);

    /// <summary>False when there was no path with that id.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
