using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Contracts.Jobs;
using RookRun.Web.Pages;
using RookRun.Web.Services;
using RookRun.Web.UnitTest.Infrastructure;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Tests for the jobs page component.
/// </summary>
public sealed class JobsPageTests : TestContext
{
    private const string SyncStravaDataJob = "SyncStravaDataJob";

    /// <summary>
    /// Verifies the page loads job options and displays the selected job description.
    /// </summary>
    [Fact]
    public void OnInitializedAsync_LoadsJobsAndShowsSelectedDescription()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "api/jobs", new List<JobInfoDto>
        {
            new("sync-strava", "Sync Strava", "Syncs activities from Strava."),
            new("export-csv", "Export CSV", "Exports all activities to CSV.")
        });

        RegisterJobsClient(handler);

        var cut = RenderComponent<Jobs>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Sync Strava", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Syncs activities from Strava.", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Verifies clicking Run Job renders the successful run result section.
    /// </summary>
    [Fact]
    public void RunSelectedJobAsync_RendersSuccessfulRunResult()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "api/jobs", new List<JobInfoDto>
        {
            new("sync-strava", "Sync Strava", "Syncs activities from Strava.")
        });
        handler.AddJsonResponse(HttpMethod.Post, "api/jobs/run", new RunJobResponse
        {
            JobName = "sync-strava",
            Succeeded = true,
            Message = "Job completed.",
            CompletedAtUtc = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero)
        });

        RegisterJobsClient(handler);

        var cut = RenderComponent<Jobs>();
        cut.WaitForAssertion(() => Assert.Contains("Run Job", cut.Markup, StringComparison.Ordinal));

        cut.Find("button.btn.btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Completed", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Job completed.", cut.Markup, StringComparison.Ordinal);
            Assert.True(handler.RequestCount >= 2);
        });
    }

    /// <summary>
    /// Verifies running the selected SyncStravaDataJob sends the composite job name to the API.
    /// </summary>
    [Fact]
    public void RunSelectedJobAsync_PostsSelectedCompositeJobName()
    {
        var observedJobName = string.Empty;
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "api/jobs", new List<JobInfoDto>
        {
            new("sync-strava-activities", "Sync Strava Activities", "Single-step sync."),
            new(SyncStravaDataJob, "Sync Strava Data", "Composite sync pipeline.")
        });
        handler.AddResponse(
            request =>
            {
                if (request.Method != HttpMethod.Post ||
                    request.RequestUri?.PathAndQuery.Equals("/api/jobs/run", StringComparison.OrdinalIgnoreCase) != true)
                {
                    return false;
                }

                var content = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                using var document = JsonDocument.Parse(content);
                if (!document.RootElement.TryGetProperty("jobName", out var jobNameProperty))
                {
                    return false;
                }

                observedJobName = jobNameProperty.GetString() ?? string.Empty;
                return true;
            },
            () => new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new RunJobResponse
                {
                    JobName = SyncStravaDataJob,
                    Succeeded = true,
                    Message = "Job started.",
                    CompletedAtUtc = new DateTimeOffset(2026, 6, 18, 0, 0, 0, TimeSpan.Zero)
                })
            });

        RegisterJobsClient(handler);

        var cut = RenderComponent<Jobs>();

        cut.WaitForAssertion(() => Assert.Contains("Sync Strava Data", cut.Markup, StringComparison.Ordinal));
        cut.Find("select#jobSelect").Change(SyncStravaDataJob);
        cut.Find("button.btn.btn-primary").Click();

        cut.WaitForAssertion(() => Assert.Equal(SyncStravaDataJob, observedJobName));
    }

    /// <summary>
    /// Verifies initialization failures are surfaced as user-visible errors.
    /// </summary>
    [Fact]
    public void OnInitializedAsync_ShowsLoadErrorWhenApiRequestFails()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddResponse(
            request => request.Method == HttpMethod.Get && request.RequestUri?.PathAndQuery.Equals("/api/jobs", StringComparison.OrdinalIgnoreCase) == true,
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        RegisterJobsClient(handler);

        var cut = RenderComponent<Jobs>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to load jobs", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Registers the typed jobs API client with a deterministic HTTP transport.
    /// </summary>
    /// <param name="handler">The fake HTTP transport used by the client.</param>
    private void RegisterJobsClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        Services.AddSingleton(new JobsApiClient(httpClient));
    }
}