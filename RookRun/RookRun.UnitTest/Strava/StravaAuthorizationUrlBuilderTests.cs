using RookRun.Strava.Auth;
using RookRun.Strava.Auth.Http;
using System.Web;

namespace RookRun.UnitTest.Strava;

public class StravaAuthorizationUrlBuilderTests
{
    [Fact]
    public void BuildAuthorizeUri_IncludesExpectedParameters()
    {
        var builder = new StravaAuthorizationUrlBuilder();
        var options = new StravaOAuthClientOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            AuthorizationBaseUrl = "https://www.strava.com/oauth",
            DefaultScopes = ["activity:read_all", "profile:read_all"],
            ApprovalPrompt = "auto"
        };

        var uri = builder.BuildAuthorizeUri(options, new Uri("http://localhost:12345/auth/strava/callback"), "state-123");
        var query = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal("https://www.strava.com/oauth/authorize", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("http://localhost:12345/auth/strava/callback", query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("auto", query["approval_prompt"]);
        Assert.Equal("activity:read_all,profile:read_all", query["scope"]);
        Assert.Equal("state-123", query["state"]);
    }

    [Fact]
    public void CreateState_ReturnsUrlSafeDistinctValues()
    {
        var builder = new StravaAuthorizationUrlBuilder();

        var first = builder.CreateState();
        var second = builder.CreateState();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
