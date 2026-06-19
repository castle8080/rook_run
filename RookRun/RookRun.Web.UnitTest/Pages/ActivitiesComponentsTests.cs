using Bunit;
using RookRun.Contracts.Strava;
using RookRun.Web.Pages;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Unit tests for Activities page child components and helpers.
/// </summary>
public sealed class ActivitiesComponentsTests : TestContext
{
    /// <summary>
    /// Verifies the controls component renders filter options and raises callbacks.
    /// </summary>
    [Fact]
    public void ActivitiesControls_RendersAndRaisesCallbacks()
    {
        var selectedType = string.Empty;
        var loadClicked = 0;
        var downloadClicked = 0;
        var selectedStart = DateTime.UtcNow.Date.AddDays(-3);
        var selectedEnd = DateTime.UtcNow.Date;

        var cut = RenderComponent<ActivitiesControls>(parameters => parameters
            .Add(p => p.StartDateUtc, selectedStart)
            .Add(p => p.StartDateUtcChanged, value => selectedStart = value)
            .Add(p => p.EndDateUtc, selectedEnd)
            .Add(p => p.EndDateUtcChanged, value => selectedEnd = value)
            .Add(p => p.SelectedTypeFilter, selectedType)
            .Add(p => p.SelectedTypeFilterChanged, value => selectedType = value)
            .Add(p => p.AvailableTypeFilters, new[] { "Ride", "Run" })
            .Add(p => p.IsLoading, false)
            .Add(p => p.IsDownloadDisabled, false)
            .Add(p => p.OnLoadActivities, () => loadClicked++)
            .Add(p => p.OnDownloadCsv, () => downloadClicked++));

        cut.Find("#typeFilter").Change("Run");
        cut.Find("button.btn.btn-primary").Click();
        cut.Find("button.btn.btn-outline-secondary").Click();

        Assert.Equal("Run", selectedType);
        Assert.Equal(1, loadClicked);
        Assert.Equal(1, downloadClicked);
    }

    /// <summary>
    /// Verifies the table component renders formatted data values.
    /// </summary>
    [Fact]
    public void ActivitiesTable_RendersFormattedActivityRow()
    {
        var activities = new[]
        {
            new StravaActivityDto
            {
                Id = 10,
                Name = "Evening Run",
                Type = "Run",
                StartDate = new DateTimeOffset(2026, 6, 5, 1, 2, 0, TimeSpan.Zero),
                StartDateLocal = new DateTimeOffset(2026, 6, 4, 19, 2, 0, TimeSpan.FromHours(-6)),
                ElapsedTime = 3661,
                MovingTime = 3520,
                Distance = 10000,
                TotalElevationGain = 120,
                AverageHeartrate = 149,
                MaxHeartrate = 168
            }
        };

        var cut = RenderComponent<ActivitiesTable>(parameters =>
            parameters.Add(p => p.Activities, activities));

        var rowText = cut.Find("tbody tr").TextContent;
        Assert.Contains("2026-06-05 01:02", rowText, StringComparison.Ordinal);
        Assert.Contains("Evening Run", rowText, StringComparison.Ordinal);
        Assert.Contains("Run", rowText, StringComparison.Ordinal);
        Assert.Contains("01:01:01", rowText, StringComparison.Ordinal);
        Assert.Contains("6.21", rowText, StringComparison.Ordinal);
        Assert.Contains("09:26", rowText, StringComparison.Ordinal);
        Assert.Contains("394", rowText, StringComparison.Ordinal);
        Assert.Contains("149", rowText, StringComparison.Ordinal);
        Assert.Contains("168", rowText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the table component shows the empty-state row when there are no activities.
    /// </summary>
    [Fact]
    public void ActivitiesTable_RendersEmptyState_WhenNoActivities()
    {
        var cut = RenderComponent<ActivitiesTable>(parameters =>
            parameters.Add(p => p.Activities, Array.Empty<StravaActivityDto>()));

        Assert.Contains("No activities match the current filters", cut.Markup, StringComparison.Ordinal);
    }

}
