using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RookRun.Job.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedTransient<IJob, TestStravaConnectivityJob>(nameof(TestStravaConnectivityJob));
        return services;
    }
}
