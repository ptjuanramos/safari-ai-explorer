using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Infrastructure.AzureStorage;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.Google;
using SafariExplorer.Infrastructure.JobData;
using SafariExplorer.Infrastructure.Spatial;

namespace SafariExplorer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The job data and the saved paths, from local folders or the storage account depending on
    /// <c>Storage:Kind</c> (ADR-0004). The choice is made when the services are first used, so settings added late
    /// (tests, environment) still count.
    /// </summary>
    public static IServiceCollection AddJobData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DataOptions>(options =>
        {
            configuration.GetSection(DataOptions.Section).Bind(options);
            if (configuration["DATA_DIR"] is { Length: > 0 } dataDir)
            {
                options.Root = dataDir;
            }
        });
        services.AddOptions<StorageOptions>()
            .Configure(options => StorageOptions.Bind(options, configuration))
            .Validate(options => options.Kind != StorageKind.Azure || options.AzureSettingsProblem() is null,
                "Storage:Kind is Azure, but neither AZURE_STORAGE_CONNECTION_STRING nor STORAGE_ACCOUNT_URL is set.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<IJobStore>(sp =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return storage.Kind == StorageKind.Azure
                ? new BlobJobStore(AzureStorageClients.Container(storage))
                : new LocalJobStore(sp.GetRequiredService<IOptions<DataOptions>>().Value.Root);
        });
        services.AddSingleton<IUserPathRepository>(sp =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return storage.Kind == StorageKind.Azure
                ? new TableUserPathRepository(AzureStorageClients.Table(storage))
                : new FileUserPathRepository(sp.GetRequiredService<IOptions<DataOptions>>());
        });
        services.AddSingleton<IRoadRiskRepository, RoadRiskRepository>();
        services.AddSingleton<ISightingsRepository, SightingsRepository>();
        services.AddSingleton<IRouteMatcher, BufferRouteMatcher>();
        return services;
    }

    /// <summary>
    /// Copies every path saved in <c>&lt;dataDir&gt;/user-paths/</c> to the storage account's table, keeping ids and
    /// dates. Saving the same id replaces the row, so running it twice is safe. Returns how many were copied.
    /// </summary>
    public static async Task<int> ImportUserPathsAsync(IConfiguration configuration, string dataDir,
        CancellationToken cancellationToken)
    {
        var storage = new StorageOptions();
        StorageOptions.Bind(storage, configuration);
        if (storage.AzureSettingsProblem() is not null)
        {
            throw new InvalidOperationException(
                "Set AZURE_STORAGE_CONNECTION_STRING or STORAGE_ACCOUNT_URL to say which table to import into.");
        }

        var source = new FileUserPathRepository(Options.Create(new DataOptions { Root = dataDir }));
        var target = new TableUserPathRepository(AzureStorageClients.Table(storage));
        var paths = await source.ListAsync(cancellationToken);
        foreach (var path in paths)
        {
            await target.SaveAsync(path, cancellationToken);
        }

        return paths.Count;
    }

    public static IServiceCollection AddGoogleDirections(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleMapsOptions>(options =>
        {
            configuration.GetSection(GoogleMapsOptions.Section).Bind(options);
            if (configuration["GOOGLE_MAPS_API_KEY"] is { Length: > 0 } key)
            {
                options.ApiKey = key;
            }
        });
        services.AddHttpClient<IDirectionsService, GoogleRoutesClient>(http => http.Timeout = TimeSpan.FromSeconds(20));
        return services;
    }
}
