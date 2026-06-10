using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RookRun.Bootstrap;
using RookRun.Job;

namespace RookRun
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var host = AppHostFactory.Create(args);
            await host.StartAsync();
            try
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Application started successfully.");
                try
                {
                    // Get job to run
                    //var jobName = "SyncStravaActivitiesJob";
                    var jobName = "StravaActivitiesExportJob";
                    //var jobName = "ProcessGoogleHealthExportJob";
                    var job = host.Services.GetRequiredKeyedService<IJob>(jobName);

                    await job.ExecuteAsync(CancellationToken.None);

                    return 0;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "An error occurred during application execution.");
                    return 1;
                }
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
