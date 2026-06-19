using System.Globalization;
using System.Net.Http.Json;
using RookRun.Contracts.Strava;

namespace RookRun.Web.Services;

/// <summary>
/// Provides typed HTTP access to Strava activities endpoints exposed by the API host.
/// </summary>
public sealed class StravaActivitiesApiClient
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivitiesApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the API base address.</param>
    public StravaActivitiesApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Loads one page of Strava activities using the supplied query request.
    /// </summary>
    /// <param name="request">The paging and filtering request.</param>
    /// <param name="cancellationToken">A token used to cancel the HTTP request.</param>
    /// <returns>The paged API response.</returns>
    public async Task<ListStravaActivitiesResponse> ListPageAsync(ListStravaActivitiesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = BuildListActivitiesUri(request);
        var response = await this.httpClient.GetFromJsonAsync<ListStravaActivitiesResponse>(uri, cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException("API returned an empty activities response payload.");
        }

        return response;
    }

    /// <summary>
    /// Loads all activities for the supplied UTC date range by paging through the API.
    /// </summary>
    /// <param name="startDateUtc">The inclusive UTC lower bound of the date range.</param>
    /// <param name="endDateUtc">The inclusive UTC upper bound of the date range.</param>
    /// <param name="cancellationToken">A token used to cancel the HTTP request.</param>
    /// <returns>All activities returned by the API for the requested date range.</returns>
    public async Task<IReadOnlyList<StravaActivityDto>> ListAllByUtcDateRangeAsync(
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        CancellationToken cancellationToken = default)
    {
        if (startDateUtc > endDateUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(startDateUtc), "startDateUtc must be less than or equal to endDateUtc.");
        }

        const int pageSize = 1000;
        var page = 1;
        var activities = new List<StravaActivityDto>();

        while (true)
        {
            var response = await ListPageAsync(new ListStravaActivitiesRequest
            {
                Page = page,
                PageSize = pageSize,
                StartDateUtc = startDateUtc,
                EndDateUtc = endDateUtc,
                SortDirection = SortDirection.Desc
            }, cancellationToken);

            activities.AddRange(response.Items);

            if (!response.HasNextPage)
            {
                break;
            }

            page++;
        }

        return activities;
    }

    /// <summary>
    /// Builds the relative URI for listing Strava activities.
    /// </summary>
    /// <param name="request">The request to convert to query string parameters.</param>
    /// <returns>The relative URI to call.</returns>
    private static string BuildListActivitiesUri(ListStravaActivitiesRequest request)
    {
        var queryParts = new List<string>();

        if (request.Page is not null)
        {
            queryParts.Add($"page={request.Page.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.PageSize is not null)
        {
            queryParts.Add($"pageSize={request.PageSize.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.StartDateUtc is not null)
        {
            queryParts.Add($"startDateUtc={Uri.EscapeDataString(request.StartDateUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (request.EndDateUtc is not null)
        {
            queryParts.Add($"endDateUtc={Uri.EscapeDataString(request.EndDateUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (!string.IsNullOrWhiteSpace(request.ActivityType))
        {
            queryParts.Add($"activityType={Uri.EscapeDataString(request.ActivityType)}");
        }

        if (request.SortDirection is not null)
        {
            queryParts.Add($"sortDirection={request.SortDirection.Value}");
        }

        return queryParts.Count == 0
            ? "api/strava/activities"
            : $"api/strava/activities?{string.Join("&", queryParts)}";
    }

    /// <summary>
    /// Fetches aggregated run statistics for a 13-month rolling window from the API.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the HTTP request.</param>
    /// <returns>The run statistics response containing YTD totals and per-month stats.</returns>
    public async Task<RunStatsResponse> GetRunStatsAsync(CancellationToken cancellationToken = default)
    {
        var response = await this.httpClient.GetFromJsonAsync<RunStatsResponse>(
            "api/strava/run-stats", cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException("API returned an empty run stats response payload.");
        }

        return response;
    }
}
