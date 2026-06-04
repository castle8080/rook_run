namespace RookRun.Garmin;

public interface IGarminActivities : IAsyncDisposable
{
    Task LoginAsync();
}
