using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="StravaActivitiesExportJob"/>.
/// </summary>
[Collection("JobTests")]
public sealed class StravaActivitiesExportJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        var repository = new Mock<IStravaActivitiesRepository>();

        Assert.Throws<ArgumentNullException>(() => new StravaActivitiesExportJob(null!, NullLogger<StravaActivitiesExportJob>.Instance));
        Assert.Throws<ArgumentNullException>(() => new StravaActivitiesExportJob(repository.Object, null!));
    }

    /// <summary>
    /// Verifies execution writes CSV output to the expected path.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WritesCsvFileToVarFolder()
    {
        var activities = new[]
        {
            new StravaActivity { Id = 1, Name = "Morning Run", Type = "Run" },
            new StravaActivity { Id = 2, Name = "Evening Ride", Type = "Ride" }
        };

        var repository = new Mock<IStravaActivitiesRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.SearchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        var originalDirectory = Environment.CurrentDirectory;
        var tempRoot = Path.Combine(Path.GetTempPath(), "rookrun-export-job-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            Environment.CurrentDirectory = tempRoot;
            var job = new StravaActivitiesExportJob(repository.Object, NullLogger<StravaActivitiesExportJob>.Instance);

            await job.ExecuteAsync(CancellationToken.None);

            var outputPath = Path.Combine(tempRoot, "var", "strava_activities.csv");
            Assert.True(File.Exists(outputPath));

            var csv = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("id", csv, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("name", csv, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Morning Run", csv, StringComparison.Ordinal);
            Assert.Contains("Evening Ride", csv, StringComparison.Ordinal);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        repository.VerifyAll();
    }
}
