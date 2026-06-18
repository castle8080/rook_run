using System.Net;

namespace RookRun.Common.Exceptions;

/// <summary>
/// Represents a precondition failure for optimistic concurrency checks.
/// Common examples include ETag or version-token mismatches when updating persisted state.
/// Maps to HTTP 412 Precondition Failed.
/// </summary>
public sealed class PreconditionFailedException : AppException
{
    private const string ErrorCodeValue = "PRECONDITION_FAILED";

    /// <summary>
    /// Initializes a new instance of the <see cref="PreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    public PreconditionFailedException(string message)
        : base(ErrorCodeValue, HttpStatusCode.PreconditionFailed, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="innerException">The underlying cause of the exception.</param>
    public PreconditionFailedException(string message, Exception innerException)
        : base(ErrorCodeValue, HttpStatusCode.PreconditionFailed, message, innerException)
    {
    }
}
