using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using RookRun.Contracts;
using RookRun.Web.Services;
using RookRun.Web.UnitTest.Infrastructure;

namespace RookRun.Web.UnitTest.Services;

/// <summary>
/// Unit tests for <see cref="ApiAuthenticationStateProvider"/>.
/// </summary>
public sealed class ApiAuthenticationStateProviderTests
{
    /// <summary>
    /// Verifies the provider calls /auth/me and returns an authenticated principal when the endpoint indicates success.
    /// </summary>
    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAuthenticatedPrincipal_WhenAuthEndpointReturnsAuthenticatedUser()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/auth/me",
            new UserAuthDto
            {
                IsAuthenticated = true,
                Email = "runner@example.com"
            },
            HttpStatusCode.OK);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var sut = new ApiAuthenticationStateProvider(httpClient);

        var state = await sut.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("runner@example.com", state.User.FindFirst(ClaimTypes.Email)?.Value);
    }

    /// <summary>
    /// Verifies the provider returns an anonymous principal when /auth/me reports unauthenticated.
    /// </summary>
    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAnonymousPrincipal_WhenAuthEndpointReturnsUnauthenticatedUser()
    {
        var handler = new StubHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/auth/me",
            new UserAuthDto
            {
                IsAuthenticated = false,
                Email = null
            },
            HttpStatusCode.OK);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var sut = new ApiAuthenticationStateProvider(httpClient);

        var state = await sut.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }
}
