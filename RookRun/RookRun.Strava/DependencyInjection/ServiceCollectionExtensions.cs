using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RookRun.Strava.Client;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Client.Auth.Browser;
using RookRun.Strava.Client.Auth.Hosting;
using RookRun.Strava.Options;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;
using System.Net.Http.Headers;

namespace RookRun.Strava.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "StravaActivities";
    private const string OAuthHttpClientName = "StravaOAuth";

    public static IServiceCollection AddStravaActivities(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<StravaClientOptions>()
            .Bind(configuration.GetSection("StravaClient"));

        services
            .AddOptions<StravaOAuthClientOptions>()
            .Bind(configuration.GetSection("StravaOAuthClient"));

        services
            .AddOptions<StravaTokenStoreOptions>()
            .Bind(configuration.GetSection("StravaTokenStore"));

        services
            .AddOptions<ObjectStoreStravaActivitiesRepositoryOptions>()
            .Bind(configuration.GetSection("StravaActivitiesRepository"));

        services.AddHttpClient(HttpClientName, static (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaClientOptions>>().Value;
            httpClient.BaseAddress = BuildBaseUri(options.ApiBaseUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient(OAuthHttpClientName, static (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaOAuthClientOptions>>().Value;
            httpClient.BaseAddress = BuildBaseUri(options.AuthorizationBaseUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<IStravaTokenStore>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StravaTokenStoreOptions>>().Value;
            return options.UseWindowsDpapi && OperatingSystem.IsWindows()
                ? new WindowsDpapiStravaTokenStore(serviceProvider.GetRequiredService<IOptions<StravaTokenStoreOptions>>())
                : new NullStravaTokenStore();
        });
        services.AddSingleton<IStravaAccessTokenProvider, RookRun.Strava.Client.Auth.StravaAccessTokenProvider>();
        services.AddSingleton<IStravaActivitiesClient, StravaActivitiesClient>();
        services.AddSingleton<IStravaActivitiesRepository, ObjectStoreStravaActivitiesRepository>();
        services.AddSingleton<StravaActivitiesSynchronizer>();
        services.AddSingleton<IStravaAuthorizationLauncher, DefaultStravaAuthorizationLauncher>();
        services.AddSingleton<IStravaOAuthListenerHost, StravaOAuthListenerHost>();
        services.AddSingleton<IStravaOAuthClient, StravaOAuthClient>();

        return services;
    }

    private static Uri BuildBaseUri(string baseUrl) => new($"{baseUrl.TrimEnd('/')}/");
}