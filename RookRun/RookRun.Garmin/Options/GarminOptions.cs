namespace RookRun.Garmin.Options;

public sealed record GarminOptions
{
    public const string SectionName = "Garmin";

    public required string BaseUrl { get; set; }

    public required string SsoBaseUrl { get; set; }

    public required string Username { get; set; }

    public required string Password { get; set; }

    public required string UserAgent { get; set; }

    public bool Headless { get; set; } = true;

    public string Browser { get; set; } = "chromium";

    public string? ProfilePath { get; set; }
}
