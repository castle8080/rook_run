using System.Net.Http.Json;
using RookRun.Contracts;

namespace RookRun.Web.Services;

/// <summary>
/// Provides HTTP access to jobs endpoints exposed by the API host.
/// </summary>
public sealed class JobsApiClient
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with API base address.</param>
    public JobsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Loads the list of available jobs from the API.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the HTTP request.</param>
    /// <returns>The jobs available for execution.</returns>
    public async Task<IReadOnlyList<JobInfoDto>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await this.httpClient.GetFromJsonAsync<List<JobInfoDto>>("api/jobs", cancellationToken);
        return jobs ?? [];
    }

    /// <summary>
    /// Sends a request to execute a job and returns the API response.
    /// </summary>
    /// <param name="jobName">The unique job name to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the HTTP request.</param>
    /// <returns>The execution result response.</returns>
    public async Task<RunJobResponse> RunJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        var request = new RunJobRequest { JobName = jobName };
        var response = await this.httpClient.PostAsJsonAsync("api/jobs/run", request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<RunJobResponse>(cancellationToken);

        if (payload is null)
        {
            throw new InvalidOperationException("API returned an empty response payload.");
        }

        return payload;
    }
}
