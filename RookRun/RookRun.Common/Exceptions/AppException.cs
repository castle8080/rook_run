using System.Net;

namespace RookRun.Common.Exceptions;

/// <summary>
/// Represents a base application exception that carries an error code and HTTP status hint.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppException"/> class.
    /// </summary>
    /// <param name="errorCode">A stable machine-readable error code.</param>
    /// <param name="statusCode">The HTTP status code associated with the exception.</param>
    /// <param name="message">The human-readable error message.</param>
    protected AppException(string errorCode, HttpStatusCode statusCode, string message)
        : base(message)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? throw new ArgumentException("Error code must be provided.", nameof(errorCode))
            : errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppException"/> class.
    /// </summary>
    /// <param name="errorCode">A stable machine-readable error code.</param>
    /// <param name="statusCode">The HTTP status code associated with the exception.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="innerException">The underlying cause of the exception.</param>
    protected AppException(string errorCode, HttpStatusCode statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? throw new ArgumentException("Error code must be provided.", nameof(errorCode))
            : errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the machine-readable error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the HTTP status code associated with this exception.
    /// </summary>
    public HttpStatusCode StatusCode { get; }
}