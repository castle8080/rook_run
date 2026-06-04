using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Garmin.Options;

namespace RookRun.Garmin.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGarminActivities(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<GarminOptions>()
            .Bind(configuration);

        services.AddSingleton<IGarminActivitiesFactory, GarminActivitiesFactory>();

        return services;
    }
}
