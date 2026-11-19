using SafariExplorer.Application.Abstractions;
using SafariExplorer.Application.UserPaths;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.UserPaths;

namespace SafariExplorer.Application.Tests;

public sealed class UserPathUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 30, 0, TimeSpan.Zero);

    private static NewUserPath Draft(int day = 1, string surface = "gravel", IReadOnlyList<GeoPoint>? stops = null) =>
        new(day, " Tarangire Gate ", "Silale Swamp",
            stops ?? [new GeoPoint(36.0167, -3.7167), new GeoPoint(36.08, -4.0)], surface, 42.5, 95);

    [Fact]
    public async Task Add_gives_the_path_an_id_and_the_time_and_saves_it()
    {
        var repository = new FakeUserPathRepository();

        var path = await new AddUserPath(repository, new FixedClock(Now)).ExecuteAsync(Draft());

        Assert.NotNull(path);
        Assert.Matches("^[0-9a-f]{32}$", path.Id);
        Assert.Equal(Now, path.CreatedAt);
        Assert.Equal("Tarangire Gate", path.Origin);
        Assert.Same(path, Assert.Single(repository.Paths.Values));
    }

    [Fact]
    public async Task Add_refuses_once_the_store_is_full()
    {
        var repository = new FakeUserPathRepository();
        for (var i = 0; i < AddUserPath.MaxPaths; i++)
        {
            repository.Paths[$"p{i}"] = Saved($"p{i}", 1, Now);
        }

        Assert.Null(await new AddUserPath(repository, new FixedClock(Now)).ExecuteAsync(Draft()));
        Assert.Equal(AddUserPath.MaxPaths, repository.Paths.Count);
    }

    [Fact]
    public async Task List_sorts_by_day_then_by_when_it_was_added()
    {
        var repository = new FakeUserPathRepository();
        repository.Paths["c"] = Saved("c", 2, Now);
        repository.Paths["b"] = Saved("b", 1, Now.AddMinutes(5));
        repository.Paths["a"] = Saved("a", 1, Now);

        var paths = await new ListUserPaths(repository).ExecuteAsync();

        Assert.Equal(["a", "b", "c"], paths.Select(p => p.Id));
    }

    [Fact]
    public async Task Delete_reports_whether_the_path_existed()
    {
        var repository = new FakeUserPathRepository();
        repository.Paths["a"] = Saved("a", 1, Now);

        Assert.True(await new DeleteUserPath(repository).ExecuteAsync("a"));
        Assert.False(await new DeleteUserPath(repository).ExecuteAsync("a"));
    }

    [Fact]
    public void A_complete_draft_is_valid()
    {
        Assert.Null(Draft().Validate());
    }

    public static TheoryData<NewUserPath> InvalidDrafts() =>
    [
        Draft(day: 0),
        Draft(day: NewUserPath.MaxDay + 1),
        Draft(surface: "tarmac"),
        Draft(stops: [new GeoPoint(36.0, -3.7)]),
        Draft(stops: [new GeoPoint(36.0, -3.7), new GeoPoint(36.0, -91)]),
        Draft(stops: [new GeoPoint(double.NaN, -3.7), new GeoPoint(36.0, -3.8)]),
        Draft() with { Origin = "  " },
        Draft() with { Destination = new string('x', NewUserPath.MaxNameLength + 1) },
        Draft() with { Stops = null },
        Draft() with { DistanceKm = double.PositiveInfinity },
        Draft() with { DurationMin = -1 },
    ];

    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void Invalid_drafts_say_why(NewUserPath draft)
    {
        Assert.NotNull(draft.Validate());
    }

    private static UserPath Saved(string id, int day, DateTimeOffset createdAt) =>
        new(id, day, "A", "B", [new GeoPoint(36.0, -3.7), new GeoPoint(36.1, -3.8)], "track", 10, 30, createdAt);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUserPathRepository : IUserPathRepository
    {
        public Dictionary<string, UserPath> Paths { get; } = [];

        public Task<IReadOnlyList<UserPath>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserPath>>(Paths.Values.ToList());

        public Task SaveAsync(UserPath path, CancellationToken cancellationToken)
        {
            Paths[path.Id] = path;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Paths.Remove(id));
    }
}
