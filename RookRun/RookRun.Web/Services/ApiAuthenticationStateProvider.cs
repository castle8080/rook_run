using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using RookRun.Contracts;

namespace RookRun.Web.Services;

/// <summary>
/// Provides authentication state by querying the server's /auth/me endpoint.
/// </summary>
public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    /// <summary>
    /// Custom claim type for authorization status.
    /// </summary>
    public const string IsAuthorizedClaimType = "auth:isauthorized";

    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiAuthenticationStateProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    public ApiAuthenticationStateProvider(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Gets the current authentication state by querying the server.
    /// </summary>
    /// <returns>An authentication state describing the current user or anonymous principal.</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await this.httpClient.GetAsync("/auth/me");
            
            if (response.IsSuccessStatusCode)
            {
                var userAuth = await response.Content.ReadFromJsonAsync<UserAuthDto>();

                if (userAuth?.IsAuthenticated ?? false)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Email, userAuth.Email ?? string.Empty),
                        new Claim(ClaimTypes.Name, userAuth.Email ?? string.Empty),
                        new Claim(IsAuthorizedClaimType, userAuth.IsAuthorized.ToString())
                    };

                    var identity = new ClaimsIdentity(claims, "ApiAuth");
                    var user = new ClaimsPrincipal(identity);
                    return new AuthenticationState(user);
                }
            }
        }
        catch
        {
            // If auth check fails, return anonymous
        }

        return new AuthenticationState(new ClaimsPrincipal());
    }
}
