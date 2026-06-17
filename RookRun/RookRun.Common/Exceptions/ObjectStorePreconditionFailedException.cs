using System.Net;

namespace RookRun.Common.Exceptions;

/// <summary>
/// Represents a precondition failure when attempting to update an object store entry.
/// Thrown when an ETag or other condition does not match the current state.
/// Maps to HTTP 412 Precondition Failed.
/// </summary>
public sealed class ObjectStorePreconditionFailedException : AppException
{
    private const string ErrorCodeValue = "OBJECT_STORE_PRECONDITION_FAILED";

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStorePreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    public ObjectStorePreconditionFailedException(string message)
        : base(ErrorCodeValue, HttpStatusCode.PreconditionFailed, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStorePreconditionFailedException"/> class.
    /// </summary>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="innerException">The underlying cause of the exception.</param>
    public ObjectStorePreconditionFailedException(string message, Exception innerException)
        : base(ErrorCodeValue, HttpStatusCode.PreconditionFailed, message, innerException)
    {
    }
}
