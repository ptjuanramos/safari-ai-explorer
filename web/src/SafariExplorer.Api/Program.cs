using SafariExplorer.Api.Endpoints;
using SafariExplorer.Application;
using SafariExplorer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// One-off: copy the paths saved as files to the storage account's table (ADR-0004), then exit.
//   dotnet run --project web/src/SafariExplorer.Api -- import-user-paths [data folder, default DATA_DIR]
if (args is ["import-user-paths", .. var rest])
{
    var dataDir = rest is [var folder] ? folder : builder.Configuration["DATA_DIR"] ?? "data";
    var imported = await SafariExplorer.Infrastructure.DependencyInjection.ImportUserPathsAsync(builder.Configuration, dataDir, CancellationToken.None);
    Console.WriteLine($"Imported {imported} saved paths from {Path.Combine(dataDir, "user-paths")}.");
    return;
}

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddJobData(builder.Configuration);
builder.Services.AddGoogleDirections(builder.Configuration);
builder.Services.Configure<RoadRiskOptions>(builder.Configuration.GetSection(RoadRiskOptions.Section));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapRoadRiskEndpoints();
app.MapSightingsEndpoints();
app.MapDirectionsEndpoints();
app.MapUserPathEndpoints();
app.Map("/api/{**rest}", () => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
