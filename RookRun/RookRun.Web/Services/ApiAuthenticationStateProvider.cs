using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using RookRun.Contracts;

namespace RookRun.Web.Services;

/// <summary>
/// Provides authentication state by querying the server's /api/auth/me endpoint.
/// </summary>
public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
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
            var response = await this.httpClient.GetAsync("/api/auth/me");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var userAuth = JsonSerializer.Deserialize<UserAuthDto>(jsonContent);
                
                if (userAuth?.IsAuthenticated ?? false)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Email, userAuth.Email ?? string.Empty),
                        new Claim(ClaimTypes.Name, userAuth.Email ?? string.Empty)
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
