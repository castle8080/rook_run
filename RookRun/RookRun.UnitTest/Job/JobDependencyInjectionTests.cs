using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RookRun.Job;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for job service registrations.
/// </summary>
public sealed class JobDependencyInjectionTests
{
    /// <summary>
    /// Verifies known keyed jobs are registered and resolvable.
    /// </summary>
    [Fact]
    public void AddJobs_RegistersKnownKeyedJobs()
    {
        var services = CreateServiceCollection();

        services.AddJobs(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        var syncJob = provider.GetRequiredKeyedService<IJob>(nameof(SyncStravaActivitiesJob));
        var exportJob = provider.GetRequiredKeyedService<IJob>(nameof(StravaActivitiesExportJob));
        var copyJob = provider.GetRequiredKeyedService<IJob>(nameof(CopyObjectStoreJob));

        Assert.IsType<SyncStravaActivitiesJob>(syncJob);
        Assert.IsType<StravaActivitiesExportJob>(exportJob);
        Assert.IsType<CopyObjectStoreJob>(copyJob);
    }

    /// <summary>
    /// Verifies keyed registrations use transient lifetime.
    /// </summary>
    [Fact]
    public void AddJobs_RegistersTransientKeyedJobs()
    {
        var services = CreateServiceCollection();

        services.AddJobs(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredKeyedService<IJob>(nameof(CopyObjectStoreJob));
        var second = provider.GetRequiredKeyedService<IJob>(nameof(CopyObjectStoreJob));

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// Creates a service collection with the dependency graph required by all jobs.
    /// </summary>
    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IObjectStore>(new InMemoryObjectStore());

        var repository = new Mock<IStravaActivitiesRepository>();
        repository
            .Setup(r => r.SearchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());
        repository
            .Setup(r => r.SaveAllAsync(It.IsAny<IReadOnlyList<StravaActivity>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = new Mock<IStravaActivitiesClient>();
        client
            .Setup(c => c.SearchActivitiesAsync(It.IsAny<StravaActivityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());

        services.AddSingleton(repository.Object);
        services.AddSingleton(client.Object);
        services.AddSingleton<StravaActivitiesSynchronizer>();

        return services;
    }
}
