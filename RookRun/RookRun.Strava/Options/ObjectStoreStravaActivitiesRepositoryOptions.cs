using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.Strava.Options;

/// <summary>
/// Configuration for the ObjectStoreStravaActivitiesRepository, which stores Strava activities in an object store (e.g. file system, cloud storage).
/// </summary>
public sealed record ObjectStoreStravaActivitiesRepositoryOptions
{

    /// <summary>
    /// Gets or sets the path prefix used for storing Strava activities in the object store.
    /// </summary>
    public string PathPrefix { get; init; } = "activities/strava";


}
