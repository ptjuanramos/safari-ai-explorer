using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Infrastructure.AzureStorage;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.JobData;

namespace SafariExplorer.Api.Tests;

/// <summary>Storage:Kind picks the adapters (ADR-0004). Building the Azure clients doesn't connect, so no emulator is needed.</summary>
public sealed class StorageSettingsTests
{
    [Fact]
    public void Files_is_the_default()
    {
        using var factory = Factory(("DATA_DIR", Path.GetTempPath()));

        Assert.IsType<LocalJobStore>(factory.Services.GetRequiredService<IJobStore>());
        Assert.IsType<FileUserPathRepository>(factory.Services.GetRequiredService<IUserPathRepository>());
    }

    [Fact]
    public void Azure_uses_the_blob_container_and_the_table()
    {
        using var factory = Factory(
            ("STORAGE_KIND", "azure"),
            ("AZURE_STORAGE_CONNECTION_STRING", "UseDevelopmentStorage=true"),
            ("Storage:Container", "test-data"));

        Assert.IsType<BlobJobStore>(factory.Services.GetRequiredService<IJobStore>());
        Assert.IsType<TableUserPathRepository>(factory.Services.GetRequiredService<IUserPathRepository>());
    }

    [Fact]
    public void Azure_without_a_connection_string_or_account_url_stops_the_start()
    {
        using var factory = Factory(("STORAGE_KIND", "Azure"));

        var error = Assert.Throws<OptionsValidationException>(() => factory.Services);
        Assert.Contains("STORAGE_ACCOUNT_URL", error.Message);
    }

    private static WebApplicationFactory<Program> Factory(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
}
