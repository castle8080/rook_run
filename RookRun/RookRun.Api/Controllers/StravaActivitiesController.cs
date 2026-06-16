using Microsoft.AspNetCore.Mvc;
using RookRun.Contracts;
using RookRun.Strava.Repositories;

namespace RookRun.Api.Controllers;

/// <summary>
/// Exposes APIs for listing persisted Strava activities.
/// </summary>
[ApiController]
[Route("api/strava/activities")]
public sealed class StravaActivitiesController : ControllerBase
{
    private readonly IStravaActivitiesRepository stravaActivitiesRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivitiesController"/> class.
    /// </summary>
    /// <param name="stravaActivitiesRepository">The repository used to query persisted Strava activities.</param>
    public StravaActivitiesController(IStravaActivitiesRepository stravaActivitiesRepository)
    {
        this.stravaActivitiesRepository = stravaActivitiesRepository ?? throw new ArgumentNullException(nameof(stravaActivitiesRepository));
    }

    /// <summary>
    /// Lists persisted Strava activities with optional filtering, ordering, and pagination.
    /// </summary>
    /// <param name="request">The query parameters used to filter, sort, and page results.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>A paged list of Strava activities.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ListStravaActivitiesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ListStravaActivitiesResponse>> ListAsync([FromQuery] ListStravaActivitiesRequest request, CancellationToken cancellationToken)
    {
        request ??= new ListStravaActivitiesRequest();

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? ListStravaActivitiesQuery.DefaultPageSize;

        if (page < 1)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationError(nameof(request.Page), "Page must be greater than or equal to 1.")));
        }

        if (pageSize < 1 || pageSize > ListStravaActivitiesQuery.MaxPageSize)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationError(nameof(request.PageSize), $"PageSize must be between 1 and {ListStravaActivitiesQuery.MaxPageSize}.")));
        }

        if (request.StartDateUtc is not null && request.EndDateUtc is not null && request.StartDateUtc > request.EndDateUtc)
        {
            return BadRequest(new ValidationProblemDetails(CreateValidationError(nameof(request.StartDateUtc), "StartDateUtc must be less than or equal to EndDateUtc.")));
        }

        var query = new ListStravaActivitiesQuery
        {
            Page = page,
            PageSize = pageSize,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            ActivityType = request.ActivityType,
            SortDirection = request.SortDirection == SortDirection.Asc
                ? StravaActivitiesSortDirection.Asc
                : StravaActivitiesSortDirection.Desc
        };

        var result = await this.stravaActivitiesRepository.ListAsync(query, cancellationToken);

        var response = new ListStravaActivitiesResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            HasPreviousPage = result.Page > 1,
            HasNextPage = result.HasNextPage,
            Items = result.Items.Select(MapActivity).ToArray()
        };

        return Ok(response);
    }

    /// <summary>
    /// Creates a model-state dictionary with one validation error.
    /// </summary>
    /// <param name="key">The model key for the validation error.</param>
    /// <param name="error">The validation error message.</param>
    /// <returns>A model-state dictionary with a single entry.</returns>
    private static Dictionary<string, string[]> CreateValidationError(string key, string error) => new(StringComparer.Ordinal)
    {
        [key] = [error]
    };

    /// <summary>
    /// Maps a Strava domain activity to the API response DTO.
    /// </summary>
    /// <param name="activity">The domain activity to map.</param>
    /// <returns>The mapped response DTO.</returns>
    private static StravaActivityDto MapActivity(RookRun.Strava.Models.StravaActivity activity) => new()
    {
        Id = activity.Id,
        Name = activity.Name,
        Type = activity.Type,
        SportType = activity.SportType,
        StartDate = activity.StartDate,
        StartDateLocal = activity.StartDateLocal,
        Distance = activity.Distance,
        MovingTime = activity.MovingTime,
        ElapsedTime = activity.ElapsedTime,
        TotalElevationGain = activity.TotalElevationGain,
        AverageSpeed = activity.AverageSpeed,
        MaxSpeed = activity.MaxSpeed,
        AverageHeartrate = activity.AverageHeartrate,
        MaxHeartrate = activity.MaxHeartrate,
        StartLatLng = activity.StartLatLng,
        EndLatLng = activity.EndLatLng,
        AdditionalData = activity.AdditionalData
    };
}
