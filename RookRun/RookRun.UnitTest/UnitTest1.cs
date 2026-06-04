using RookRun.Strava.Models;
using System.Reflection;

namespace RookRun.UnitTest;

public class UnitTest1
{
    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPageIsZero()
    {
        var method = typeof(RookRun.Strava.StravaActivities)
            .GetMethod("BuildActivitiesUri", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new StravaActivityQuery { Page = 0 }]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public void SearchActivitiesAsync_ThrowsWhenPerPageIsOutOfRange()
    {
        var method = typeof(RookRun.Strava.StravaActivities)
            .GetMethod("BuildActivitiesUri", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new StravaActivityQuery { PerPage = 201 }]));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }
}
