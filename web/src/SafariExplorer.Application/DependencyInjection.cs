using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafariExplorer.Application.Directions;
using SafariExplorer.Application.RoadRisk;
using SafariExplorer.Application.Sightings;
using SafariExplorer.Application.UserPaths;

namespace SafariExplorer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetRoadRiskByDate>();
        services.AddScoped<ListRoadRiskDates>();
        services.AddScoped<ListRoadRiskPlaces>();
        services.AddScoped<GetRouteRisk>();
        services.AddScoped<GetDrivingRoute>();
        services.AddScoped<GetLatestSightingsRun>();
        services.AddScoped<GetSightingsTile>();
        services.AddScoped<ListUserPaths>();
        services.AddScoped<AddUserPath>();
        services.AddScoped<DeleteUserPath>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
