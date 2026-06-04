using Microsoft.Extensions.Logging;
using RookRun.Garmin;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RookRun.Job;

public class TestGarminLoginJob : IJob
{
    private readonly ILogger<TestGarminLoginJob> _logger;
    private readonly IGarminActivitiesFactory _garminActivitiesFactory;

    public TestGarminLoginJob(
        ILogger<TestGarminLoginJob> logger,
        IGarminActivitiesFactory garminActivitiesFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _garminActivitiesFactory = garminActivitiesFactory ?? throw new ArgumentNullException(nameof(garminActivitiesFactory));
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Garmin login test job.");
        await using var garminActivities = await _garminActivitiesFactory.CreateAsync();

        Console.WriteLine("Have garmin activities");

        return;
    }
}
