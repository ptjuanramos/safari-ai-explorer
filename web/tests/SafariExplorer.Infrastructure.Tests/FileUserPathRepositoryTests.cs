using Microsoft.Extensions.Options;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.UserPaths;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Tests;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class FileUserPathRepositoryTests : IDisposable
{
    private readonly TestDataFolder _data = new();

    private FileUserPathRepository Repository => new(Options.Create(new DataOptions { Root = _data.Root }));

    private string Folder => Path.Combine(_data.Root, FileUserPathRepository.Folder);

    public void Dispose() => _data.Dispose();

    private static UserPath Path1(string id = "0123abcd", string origin = "Tarangire Gate") => new(
        id, 2, origin, "Silale Swamp", [new GeoPoint(36.0167, -3.7167), new GeoPoint(36.08, -4.0)], "track", 42.5, 95,
        new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task No_folder_means_no_paths()
    {
        Assert.Empty(await Repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_saved_path_reads_back_the_same_from_a_snake_case_file()
    {
        await Repository.SaveAsync(Path1(), CancellationToken.None);

        var read = Assert.Single(await Repository.ListAsync(CancellationToken.None));
        Assert.Equal(Path1() with { Stops = read.Stops }, read);
        Assert.Equal(Path1().Stops, read.Stops);

        var json = await File.ReadAllTextAsync(Path.Combine(Folder, "0123abcd.json"));
        Assert.Contains("\"day_number\":2", json);
        Assert.Contains("\"version\":1", json);
        Assert.Contains("\"lat\":-3.7167", json);
        Assert.Equal(["0123abcd.json"], Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Saving_the_same_id_replaces_the_file()
    {
        await Repository.SaveAsync(Path1(), CancellationToken.None);
        await Repository.SaveAsync(Path1(origin: "Kuro"), CancellationToken.None);

        Assert.Equal("Kuro", Assert.Single(await Repository.ListAsync(CancellationToken.None)).Origin);
    }

    [Fact]
    public async Task Delete_removes_the_file_once()
    {
        await Repository.SaveAsync(Path1(), CancellationToken.None);

        Assert.True(await Repository.DeleteAsync("0123abcd", CancellationToken.None));
        Assert.False(await Repository.DeleteAsync("0123abcd", CancellationToken.None));
        Assert.Empty(await Repository.ListAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("../road-risk/latest")]
    [InlineData("..")]
    [InlineData("A1")]
    [InlineData("a.b")]
    public async Task Ids_that_are_not_slugs_never_touch_the_disk(string id)
    {
        _data.WithLatest("road-risk", "2026-11-15");

        Assert.False(await Repository.DeleteAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => Repository.SaveAsync(Path1(id), CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(_data.Root, "road-risk", "latest.json")));
    }

    [Fact]
    public async Task Temp_files_and_broken_files_are_skipped()
    {
        await Repository.SaveAsync(Path1(), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(Folder, ".0123abcd.tmp"), "{");
        await File.WriteAllTextAsync(Path.Combine(Folder, "broken.json"), "{ not json");
        await File.WriteAllTextAsync(Path.Combine(Folder, "Not A Slug.json"), "{}");

        Assert.Equal("0123abcd", Assert.Single(await Repository.ListAsync(CancellationToken.None)).Id);
    }
}
