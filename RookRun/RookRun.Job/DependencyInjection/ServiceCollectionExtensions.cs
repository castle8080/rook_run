using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RookRun.Job;

namespace RookRun.Job.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IJobExecutionTracker, InProcessJobExecutionTracker>();
        services.AddKeyedTransient<IJob, SyncStravaActivitiesJob>(nameof(SyncStravaActivitiesJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityDetailJob>(nameof(SyncStravaActivityDetailJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityStreamsJob>(nameof(SyncStravaActivityStreamsJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityIdImageIdIndexJob>(nameof(SyncStravaActivityIdImageIdIndexJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityImageJob>(nameof(SyncStravaActivityImageJob));
        services.AddKeyedTransient<IJob>(JobNames.SyncStravaDataJob, (serviceProvider, _) =>
        {
            var childJobs = new List<IJob>
            {
                serviceProvider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivitiesJob)),
                serviceProvider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivityDetailJob)),
                serviceProvider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivityStreamsJob)),
                serviceProvider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivityIdImageIdIndexJob)),
                serviceProvider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivityImageJob))
            };

            var logger = serviceProvider.GetRequiredService<ILogger<SequentialCompositeJob>>();
            return new SequentialCompositeJob(logger, childJobs);
        });
        services.AddKeyedTransient<IJob, StravaActivitiesExportJob>(nameof(StravaActivitiesExportJob));
        services.AddKeyedTransient<IJob, CopyObjectStoreJob>(nameof(CopyObjectStoreJob));
        return services;
    }
}
