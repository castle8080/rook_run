using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RookRun.Strava.Options;
using System.Net.Http.Headers;

namespace RookRun.Strava.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "StravaActivities";

    public static IServiceCollection AddStravaActivities(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<StravaOptions>()
            .Bind(configuration);

        services.AddHttpClient(HttpClientName, static (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaOptions>>().Value;
            httpClient.BaseAddress = BuildBaseUri(options.ApiBaseUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<IStravaActivities, StravaActivities>();

        return services;
    }

    private static Uri BuildBaseUri(string baseUrl) => new($"{baseUrl.TrimEnd('/')}/");
}