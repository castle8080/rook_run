using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RookRun.Strava.Auth;
using RookRun.Strava.Auth.Browser;
using RookRun.Strava.Auth.Hosting;
using RookRun.Strava.Options;
using System.Net.Http.Headers;

namespace RookRun.Strava.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "StravaActivities";
    private const string OAuthHttpClientName = "StravaOAuth";

    public static IServiceCollection AddStravaActivities(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<StravaOptions>()
            .Bind(configuration);

        services
            .AddOptions<StravaOAuthClientOptions>()
            .Bind(configuration);

        services.AddHttpClient(HttpClientName, static (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaOptions>>().Value;
            httpClient.BaseAddress = BuildBaseUri(options.ApiBaseUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient(OAuthHttpClientName, static (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaOAuthClientOptions>>().Value;
            httpClient.BaseAddress = BuildBaseUri(options.AuthorizationBaseUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<IStravaActivities, StravaActivities>();
        services.AddSingleton<IStravaAuthorizationLauncher, DefaultStravaAuthorizationLauncher>();
        services.AddSingleton<IStravaOAuthListenerHost, StravaOAuthListenerHost>();
        services.AddSingleton<IStravaOAuthClient, StravaOAuthClient>();

        return services;
    }

    private static Uri BuildBaseUri(string baseUrl) => new($"{baseUrl.TrimEnd('/')}/");
}