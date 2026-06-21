using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RookRun.Web.Pages;
using RookRun.Web.Services;

namespace RookRun.Web.UnitTest.Pages;

/// <summary>
/// Unit tests for <see cref="ProtectedPageContent"/>.
/// </summary>
public sealed class ProtectedPageContentTests : TestContext
{
    /// <summary>
    /// Verifies unauthenticated users are redirected to the root page.
    /// </summary>
    [Fact]
    public void Render_RedirectsToRoot_WhenUserIsNotAuthenticated()
    {
        var authContext = this.AddTestAuthorization();
        authContext.SetNotAuthorized();

        _ = RenderProtectedContent();

        var navigationManager = this.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/", navigationManager.Uri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies authenticated users without authorization are redirected to access denied.
    /// </summary>
    [Fact]
    public void Render_RedirectsToAccessDenied_WhenUserIsNotAuthorized()
    {
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("runner@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.Email, "runner@example.com"));

        _ = RenderProtectedContent();

        var navigationManager = this.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/access-denied", navigationManager.Uri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies authorized users see protected child content.
    /// </summary>
    [Fact]
    public void Render_RendersChildContent_WhenUserIsAuthorized()
    {
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("runner@example.com");
        authContext.SetClaims(
            new Claim(ClaimTypes.Email, "runner@example.com"),
            new Claim(ApiAuthenticationStateProvider.IsAuthorizedClaimType, "True"));

        var cut = RenderProtectedContent();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Protected marker", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Renders the protected content wrapper with deterministic child markup.
    /// </summary>
    /// <returns>The rendered component under test.</returns>
    private IRenderedComponent<ProtectedPageContent> RenderProtectedContent()
    {
        return RenderComponent<ProtectedPageContent>(parameters =>
            parameters.Add(component => component.ChildContent, (RenderFragment)(builder =>
            {
                builder.AddMarkupContent(0, "<p>Protected marker</p>");
            })));
    }
}