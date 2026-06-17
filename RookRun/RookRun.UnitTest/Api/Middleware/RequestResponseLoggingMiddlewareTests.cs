using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using RookRun.Api.Middleware;

namespace RookRun.UnitTest.Api.Middleware;

/// <summary>
/// Unit tests for <see cref="RequestResponseLoggingMiddleware"/>.
/// </summary>
public class RequestResponseLoggingMiddlewareTests
{
    /// <summary>
    /// Verifies that the middleware logs structured request and response data without buffering the request body.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LogsControllerActionAndResponseMetrics()
    {
        var logger = new CapturingLogger<RequestResponseLoggingMiddleware>();
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        var sut = new RequestResponseLoggingMiddleware(_ => WriteResponseAsync(), logger);

        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/jobs/run";
        context.Request.ContentLength = 13;
        context.Response.Body = responseBody;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor
            {
                ControllerName = "Jobs",
                ActionName = "RunJob"
            }),
            "JobsController.RunJob"));

        await sut.InvokeAsync(context);

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal("hello response", await ReadBodyAsync(responseBody));
        Assert.Equal(2, logger.Entries.Count);

        var executingEntry = logger.Entries[0];
        Assert.Equal(LogLevel.Information, executingEntry.LogLevel);
        Assert.Equal("POST", GetStructuredValue(executingEntry.State, "Method"));
        Assert.Equal("/api/jobs/run", GetStructuredValue(executingEntry.State, "Path"));
        Assert.Equal("Jobs", GetStructuredValue(executingEntry.State, "Controller"));
        Assert.Equal("RunJob", GetStructuredValue(executingEntry.State, "Action"));
        Assert.Equal(13L, GetStructuredValue(executingEntry.State, "RequestSizeBytes"));
        Assert.Contains("Executing POST /api/jobs/run for Jobs.RunJob", executingEntry.Message);

        var completedEntry = logger.Entries[1];
        Assert.Equal(LogLevel.Information, completedEntry.LogLevel);
        Assert.Equal("/api/jobs/run", GetStructuredValue(completedEntry.State, "Path"));
        Assert.Equal("Jobs", GetStructuredValue(completedEntry.State, "Controller"));
        Assert.Equal("RunJob", GetStructuredValue(completedEntry.State, "Action"));
        Assert.Equal(201, GetStructuredValue(completedEntry.State, "StatusCode"));
        Assert.Equal(14L, GetStructuredValue(completedEntry.State, "ResponseSizeBytes"));
        Assert.True(Convert.ToInt64(GetStructuredValue(completedEntry.State, "ElapsedMilliseconds")) >= 0);
        Assert.Contains("Completed POST /api/jobs/run for Jobs.RunJob", completedEntry.Message);

        async Task WriteResponseAsync()
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response.WriteAsync("hello response");
        }
    }

    /// <summary>
    /// Verifies that the middleware leaves request size unset when the client does not send Content-Length.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LogsNullRequestSize_WhenContentLengthIsMissing()
    {
        var logger = new CapturingLogger<RequestResponseLoggingMiddleware>();
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        var sut = new RequestResponseLoggingMiddleware(_ => WriteResponseAsync(), logger);

        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/jobs";
        context.Response.Body = responseBody;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor
            {
                ControllerName = "Jobs",
                ActionName = "GetJobs"
            }),
            "JobsController.GetJobs"));

        await sut.InvokeAsync(context);

        var executingEntry = logger.Entries[0];
        Assert.Null(GetStructuredValue(executingEntry.State, "RequestSizeBytes"));

        async Task WriteResponseAsync()
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("ok");
        }
    }

    private static async Task<string> ReadBodyAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static object? GetStructuredValue(object? state, string key)
    {
        var values = state as IReadOnlyList<KeyValuePair<string, object?>>;

        if (values is null)
        {
            return null;
        }

        return values.FirstOrDefault(pair => pair.Key == key).Value;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add(new LogEntry(logLevel, eventId, state, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, EventId EventId, object? State, Exception? Exception, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}