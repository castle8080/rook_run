using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Moq;
using RookRun.Api.Authentication;
using RookRun.Api.Controllers;

namespace RookRun.UnitTest.Api.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthController"/>.
/// </summary>
public sealed class AuthControllerTests
{
    /// <summary>
    /// Verifies unauthenticated users return an unauthenticated auth DTO.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthenticated_WhenUserIsAnonymous()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var sut = CreateController(authorizationService.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await sut.GetCurrentUser();

        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Null(result.Email);
        authorizationService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies authenticated allowlisted users return authorized auth DTO values.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsAuthorized_WhenPolicySucceeds()
    {
        var principal = CreateAuthenticatedPrincipal("runner@example.com");
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        authorizationService
            .Setup(service => service.AuthorizeAsync(principal, null, RookRunAuthorizationPolicyNames.AllowlistedUser))
            .ReturnsAsync(AuthorizationResult.Success());

        var sut = CreateController(authorizationService.Object, principal);

        var result = await sut.GetCurrentUser();

        Assert.True(result.IsAuthenticated);
        Assert.True(result.IsAuthorized);
        Assert.Equal("runner@example.com", result.Email);
        authorizationService.Verify(
            service => service.AuthorizeAsync(principal, null, RookRunAuthorizationPolicyNames.AllowlistedUser),
            Times.Once);
        authorizationService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies authenticated non-allowlisted users return unauthorized auth DTO values.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_WhenPolicyFails()
    {
        var principal = CreateAuthenticatedPrincipal("runner@example.com");
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        authorizationService
            .Setup(service => service.AuthorizeAsync(principal, null, RookRunAuthorizationPolicyNames.AllowlistedUser))
            .ReturnsAsync(AuthorizationResult.Failed());

        var sut = CreateController(authorizationService.Object, principal);

        var result = await sut.GetCurrentUser();

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Equal("runner@example.com", result.Email);
        authorizationService.Verify(
            service => service.AuthorizeAsync(principal, null, RookRunAuthorizationPolicyNames.AllowlistedUser),
            Times.Once);
        authorizationService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies sign-in keeps a local return URL.
    /// </summary>
    [Fact]
    public void SignIn_UsesProvidedReturnUrl_WhenReturnUrlIsLocal()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var urlHelper = new Mock<IUrlHelper>(MockBehavior.Strict);
        urlHelper.Setup(helper => helper.IsLocalUrl("/jobs")).Returns(true);

        var sut = CreateController(
            authorizationService.Object,
            new ClaimsPrincipal(new ClaimsIdentity()),
            urlHelper.Object);

        var result = sut.SignIn("/jobs");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Single(challenge.AuthenticationSchemes);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, challenge.AuthenticationSchemes[0]);
        Assert.NotNull(challenge.Properties);
        Assert.Equal("/jobs", challenge.Properties!.RedirectUri);
        urlHelper.Verify(helper => helper.IsLocalUrl("/jobs"), Times.Once);
        urlHelper.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies sign-in rewrites a non-local return URL to root.
    /// </summary>
    [Fact]
    public void SignIn_UsesRootReturnUrl_WhenReturnUrlIsNotLocal()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var urlHelper = new Mock<IUrlHelper>(MockBehavior.Strict);
        urlHelper.Setup(helper => helper.IsLocalUrl("https://evil.example")).Returns(false);

        var sut = CreateController(
            authorizationService.Object,
            new ClaimsPrincipal(new ClaimsIdentity()),
            urlHelper.Object);

        var result = sut.SignIn("https://evil.example");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.NotNull(challenge.Properties);
        Assert.Equal("/", challenge.Properties!.RedirectUri);
        urlHelper.Verify(helper => helper.IsLocalUrl("https://evil.example"), Times.Once);
        urlHelper.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Verifies sign-out rewrites a non-local return URL to root.
    /// </summary>
    [Fact]
    public void SignOut_UsesRootReturnUrl_WhenReturnUrlIsNotLocal()
    {
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var urlHelper = new Mock<IUrlHelper>(MockBehavior.Strict);
        urlHelper.Setup(helper => helper.IsLocalUrl("https://evil.example")).Returns(false);

        var sut = CreateController(
            authorizationService.Object,
            new ClaimsPrincipal(new ClaimsIdentity()),
            urlHelper.Object);

        var result = sut.SignOut("https://evil.example");

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.NotNull(signOut.Properties);
        Assert.Equal("/", signOut.Properties!.RedirectUri);
        urlHelper.Verify(helper => helper.IsLocalUrl("https://evil.example"), Times.Once);
        urlHelper.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Creates the controller with a configured HttpContext user.
    /// </summary>
    /// <param name="authorizationService">The authorization service dependency.</param>
    /// <param name="user">The user principal to assign to HttpContext.</param>
    /// <returns>A controller configured for test execution.</returns>
    private static AuthController CreateController(
        IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        IUrlHelper? urlHelper = null)
    {
        var controller = new AuthController(authorizationService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            },
            Url = urlHelper ?? new Mock<IUrlHelper>(MockBehavior.Loose).Object
        };

        return controller;
    }

    /// <summary>
    /// Creates an authenticated principal with an email claim.
    /// </summary>
    /// <param name="email">The email claim value.</param>
    /// <returns>An authenticated principal.</returns>
    private static ClaimsPrincipal CreateAuthenticatedPrincipal(string email)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("email", email),
            new Claim("name", "Runner")
        ],
        authenticationType: "oidc");

        return new ClaimsPrincipal(identity);
    }
}