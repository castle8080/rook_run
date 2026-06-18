using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Job;

namespace RookRun.Job.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IJobExecutionTracker, InProcessJobExecutionTracker>();
        services.AddKeyedTransient<IJob, SyncStravaActivitiesJob>(nameof(SyncStravaActivitiesJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityDetailJob>(nameof(SyncStravaActivityDetailJob));
        services.AddKeyedTransient<IJob, SyncStravaActivityImageJob>(nameof(SyncStravaActivityImageJob));
        services.AddKeyedTransient<IJob, StravaActivitiesExportJob>(nameof(StravaActivitiesExportJob));
        services.AddKeyedTransient<IJob, CopyObjectStoreJob>(nameof(CopyObjectStoreJob));
        return services;
    }
}
