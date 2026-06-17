using Microsoft.AspNetCore.Mvc;
using Moq;
using RookRun.Api.Controllers;
using RookRun.Contracts.Strava;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Unit tests for <see cref="StravaActivitiesController"/>.
/// </summary>
public class StravaActivitiesControllerTests
{
    /// <summary>
    /// Verifies that listing activities returns a bad request when page is less than one.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsBadRequest_WhenPageIsLessThanOne()
    {
        var repository = new Mock<IStravaActivitiesRepository>();
        var sut = new StravaActivitiesController(repository.Object);

        var result = await sut.ListAsync(new ListStravaActivitiesRequest { Page = 0 }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Contains("Page", details.Errors.Keys, StringComparer.Ordinal);
        repository.Verify(x => x.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that listing activities returns a bad request when page size is outside limits.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsBadRequest_WhenPageSizeIsOutOfRange()
    {
        var repository = new Mock<IStravaActivitiesRepository>();
        var sut = new StravaActivitiesController(repository.Object);

        var result = await sut.ListAsync(
            new ListStravaActivitiesRequest { Page = 1, PageSize = ListStravaActivitiesQuery.MaxPageSize + 1 },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Contains("PageSize", details.Errors.Keys, StringComparer.Ordinal);
        repository.Verify(x => x.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that listing activities returns a bad request when start date is after end date.
    /// </summary>
    [Fact]
    public async Task ListAsync_ReturnsBadRequest_WhenStartDateIsAfterEndDate()
    {
        var repository = new Mock<IStravaActivitiesRepository>();
        var sut = new StravaActivitiesController(repository.Object);

        var result = await sut.ListAsync(
            new ListStravaActivitiesRequest
            {
                StartDateUtc = new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero),
                EndDateUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Contains("StartDateUtc", details.Errors.Keys, StringComparer.Ordinal);
        repository.Verify(x => x.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that null requests use default paging and descending sort when querying the repository.
    /// </summary>
    [Fact]
    public async Task ListAsync_UsesDefaults_WhenRequestIsNull()
    {
        var repository = new Mock<IStravaActivitiesRepository>();

        repository
            .Setup(x => x.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStravaActivitiesResult
            {
                Page = 1,
                PageSize = ListStravaActivitiesQuery.DefaultPageSize,
                HasNextPage = false,
                Items = []
            });

        var sut = new StravaActivitiesController(repository.Object);

        var result = await sut.ListAsync(null!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ListStravaActivitiesResponse>(ok.Value);

        Assert.Equal(1, response.Page);
        Assert.Equal(ListStravaActivitiesQuery.DefaultPageSize, response.PageSize);
        Assert.False(response.HasPreviousPage);
        Assert.False(response.HasNextPage);

        repository.Verify(
            x => x.ListAsync(
                It.Is<ListStravaActivitiesQuery>(query =>
                    query.Page == 1 &&
                    query.PageSize == ListStravaActivitiesQuery.DefaultPageSize &&
                    query.SortDirection == StravaActivitiesSortDirection.Desc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that listing activities maps repository items and sort direction to the API response.
    /// </summary>
    [Fact]
    public async Task ListAsync_MapsRepositoryResultAndUsesAscendingSort()
    {
        var repository = new Mock<IStravaActivitiesRepository>();
        var startDate = new DateTimeOffset(2026, 5, 1, 6, 0, 0, TimeSpan.Zero);

        repository
            .Setup(x => x.ListAsync(It.IsAny<ListStravaActivitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListStravaActivitiesResult
            {
                Page = 2,
                PageSize = 1,
                HasNextPage = true,
                Items =
                [
                    new StravaActivity
                    {
                        Id = 42,
                        Name = "Morning Run",
                        Type = "Run",
                        SportType = "Trail Run",
                        StartDate = startDate,
                        StartDateLocal = startDate,
                        StartLatLng = [47.1, -122.1],
                        EndLatLng = [47.2, -122.2]
                    }
                ]
            });

        var sut = new StravaActivitiesController(repository.Object);

        var request = new ListStravaActivitiesRequest
        {
            Page = 2,
            PageSize = 1,
            SortDirection = SortDirection.Asc,
            ActivityType = "run"
        };

        var result = await sut.ListAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ListStravaActivitiesResponse>(ok.Value);

        Assert.Equal(2, response.Page);
        Assert.Equal(1, response.PageSize);
        Assert.True(response.HasPreviousPage);
        Assert.True(response.HasNextPage);
        Assert.Single(response.Items);
        Assert.Equal(42, response.Items[0].Id);
        Assert.Equal("Morning Run", response.Items[0].Name);
        Assert.NotNull(response.Items[0].StartLatLng);
        Assert.Equal([47.1, -122.1], response.Items[0].StartLatLng!);

        repository.Verify(
            x => x.ListAsync(
                It.Is<ListStravaActivitiesQuery>(query =>
                    query.Page == 2 &&
                    query.PageSize == 1 &&
                    query.ActivityType == "run" &&
                    query.SortDirection == StravaActivitiesSortDirection.Asc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
