using RookRun.Strava.Client.Auth;
using System.Text.Json;

namespace RookRun.UnitTest.Strava;

public class StravaOAuthClientTests
{
    [Fact]
    public void MapResult_PreservesScopeAthleteAndAdditionalFields()
    {
        const string json = """
            {
              "token_type": "Bearer",
              "access_token": "access-token",
              "refresh_token": "refresh-token",
              "expires_at": 1735689600,
              "scope": "activity:read_all,profile:read_all",
              "athlete": {
                "id": 42
              },
              "foo": "bar"
            }
            """;

        using var document = JsonDocument.Parse(json);
        var result = StravaOAuthClient.MapResult(document, json);

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(1735689600, result.ExpiresAtUnixTimeSeconds);
        Assert.Equal(["activity:read_all", "profile:read_all"], result.GrantedScopes);
        Assert.Equal("activity:read_all,profile:read_all", result.RawScope);
        Assert.True(result.Athlete.HasValue);
        Assert.Equal(42, result.Athlete.Value.GetProperty("id").GetInt32());
        Assert.Equal("bar", result.AdditionalData["foo"].GetString());
        Assert.Equal(json, result.RawResponseJson);
    }

      [Fact]
      public void ParseGrantedScopes_ParsesDistinctTrimmedScopes()
      {
        const string json = """
          {
            "scope": "activity:read_all, profile:read_all,activity:read_all"
          }
          """;

        using var document = JsonDocument.Parse(json);
        var scopes = StravaOAuthClient.ParseGrantedScopes(document.RootElement);

        Assert.Equal(["activity:read_all", "profile:read_all"], scopes);
      }

      [Fact]
      public void ParseGrantedScopes_ReturnsEmptyWhenScopeMissingOrBlank()
      {
        using var missingScopeDocument = JsonDocument.Parse("{}");
        var missingScope = StravaOAuthClient.ParseGrantedScopes(missingScopeDocument.RootElement);

        using var blankScopeDocument = JsonDocument.Parse("""{ "scope": "  " }""");
        var blankScope = StravaOAuthClient.ParseGrantedScopes(blankScopeDocument.RootElement);

        Assert.Empty(missingScope);
        Assert.Empty(blankScope);
      }

      [Fact]
      public void MapResult_ThrowsWhenRequiredFieldsAreMissing()
      {
        const string json = """
          {
            "token_type": "Bearer"
          }
          """;

        using var document = JsonDocument.Parse(json);

        Assert.Throws<KeyNotFoundException>(() => StravaOAuthClient.MapResult(document, json));
      }
}
