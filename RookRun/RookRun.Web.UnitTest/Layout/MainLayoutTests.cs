using Bunit;
using Microsoft.AspNetCore.Components;
using RookRun.Web.Layout;

namespace RookRun.Web.UnitTest.Layout;

/// <summary>
/// Unit tests for <see cref="MainLayout"/>.
/// </summary>
public sealed class MainLayoutTests : TestContext
{
    /// <summary>
    /// Verifies the layout renders a sign-out link to the host authentication endpoint.
    /// </summary>
    [Fact]
    public void Render_RendersSignOutLink()
    {
        var cut = RenderComponent<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Body</p>"))));

        Assert.Contains("href=\"/auth/sign-out\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Sign out", cut.Markup, StringComparison.Ordinal);
    }
}
