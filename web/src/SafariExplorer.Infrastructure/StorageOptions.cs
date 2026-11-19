using Microsoft.Extensions.Configuration;

namespace SafariExplorer.Infrastructure;

public enum StorageKind
{
    /// <summary>The jobs' files and the saved paths in <c>DATA_DIR</c> (ADR-0002, ADR-0003).</summary>
    Files,

    /// <summary>The jobs' files in a blob container and the saved paths in a table (ADR-0004).</summary>
    Azure,
}

/// <summary>Where the API reads the job data and keeps the saved paths (section <c>Storage</c>).</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary><see cref="StorageKind.Files"/> (default) or <see cref="StorageKind.Azure"/>; env <c>STORAGE_KIND</c>.</summary>
    public StorageKind Kind { get; set; } = StorageKind.Files;

    /// <summary>
    /// Used when set (Azurite: <c>UseDevelopmentStorage=true</c>); env <c>AZURE_STORAGE_CONNECTION_STRING</c>.
    /// Otherwise <see cref="AccountUrl"/> with <c>DefaultAzureCredential</c>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The account's blob endpoint, <c>https://&lt;account&gt;.blob.core.windows.net</c>; env <c>STORAGE_ACCOUNT_URL</c>.
    /// The table endpoint is the same with <c>.table.</c>.
    /// </summary>
    public string? AccountUrl { get; set; }

    /// <summary>The blob container holding the job files, under the same keys as the folders.</summary>
    public string Container { get; set; } = "job-data";

    /// <summary>The table holding one row per saved path.</summary>
    public string UserPathsTable { get; set; } = "userpaths";

    /// <summary>Binds the section, then lets the environment variables override it, as <c>DATA_DIR</c> does.</summary>
    public static void Bind(StorageOptions options, IConfiguration configuration)
    {
        configuration.GetSection(Section).Bind(options);
        if (configuration["STORAGE_KIND"] is { Length: > 0 } kind)
        {
            options.Kind = Enum.Parse<StorageKind>(kind, ignoreCase: true);
        }

        if (configuration["AZURE_STORAGE_CONNECTION_STRING"] is { Length: > 0 } connectionString)
        {
            options.ConnectionString = connectionString;
        }

        if (configuration["STORAGE_ACCOUNT_URL"] is { Length: > 0 } accountUrl)
        {
            options.AccountUrl = accountUrl;
        }
    }

    /// <summary>Why the Azure settings can't work, or null when they can.</summary>
    public string? AzureSettingsProblem() =>
        string.IsNullOrWhiteSpace(ConnectionString) && string.IsNullOrWhiteSpace(AccountUrl)
            ? "Storage:Kind is Azure, but neither AZURE_STORAGE_CONNECTION_STRING nor STORAGE_ACCOUNT_URL is set."
            : null;
}
