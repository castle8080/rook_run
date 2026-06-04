namespace RookRun.Strava.Options;

public sealed record StravaOptions
{
    public const string SectionName = "Strava";

    public required string ApiBaseUrl { get; set; }

    public required string AuthorizationBaseUrl { get; set; }

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    public required string RefreshToken { get; set; }
}