using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace RookRun.Api.Middleware;

/// <summary>
/// Logs controller endpoint execution details without buffering request bodies.
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestResponseLoggingMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestResponseLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger used to emit request and response telemetry.</param>
    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware for the current request.
    /// </summary>
    /// <param name="context">The HTTP context for the active request.</param>
    /// <returns>A task that completes when request processing finishes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.GetEndpoint();
        var actionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var controllerName = actionDescriptor?.ControllerName ?? endpoint?.DisplayName ?? "unknown";
        var actionName = actionDescriptor?.ActionName ?? endpoint?.DisplayName ?? "unknown";
        var requestSizeBytes = context.Request.ContentLength;
        var originalResponseBody = context.Response.Body;
        await using var responseBody = new CountingWriteStream(originalResponseBody);
        context.Response.Body = responseBody;

        var stopwatch = Stopwatch.StartNew();

        this.logger.LogInformation(
            "Executing {Method} {Path} for {Controller}.{Action}. RequestSizeBytes={RequestSizeBytes}",
            context.Request.Method,
            context.Request.Path.Value,
            controllerName,
            actionName,
            requestSizeBytes.HasValue ? requestSizeBytes.Value : null);

        try
        {
            await this.next(context);
        }
        finally
        {
            stopwatch.Stop();
            context.Response.Body = originalResponseBody;

            this.logger.LogInformation(
                "Completed {Method} {Path} for {Controller}.{Action} in {ElapsedMilliseconds} ms with {StatusCode} and ResponseSizeBytes={ResponseSizeBytes}",
                context.Request.Method,
                context.Request.Path.Value,
                controllerName,
                actionName,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode,
                responseBody.BytesWritten);
        }
    }

    private sealed class CountingWriteStream : Stream
    {
        private readonly Stream innerStream;

        /// <summary>
        /// Gets the total number of bytes written to the wrapped response stream.
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CountingWriteStream"/> class.
        /// </summary>
        /// <param name="innerStream">The response stream to wrap.</param>
        public CountingWriteStream(Stream innerStream)
        {
            this.innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
            this.innerStream.Flush();
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.innerStream.FlushAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.BytesWritten += count;
            this.innerStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.BytesWritten += buffer.Length;
            await this.innerStream.WriteAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            this.BytesWritten += count;
            return this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}