using System.Net;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Represents an Asterisk ARI operation failure.
/// </summary>
internal sealed class AsteriskAriException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriException"/> class.
    /// </summary>
    public AsteriskAriException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public AsteriskAriException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AsteriskAriException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriException"/> class.
    /// </summary>
    /// <param name="operation">The ARI operation that failed.</param>
    /// <param name="statusCode">The HTTP status code returned by Asterisk, when available.</param>
    /// <param name="message">The exception message.</param>
    public AsteriskAriException(
        string operation,
        HttpStatusCode? statusCode,
        string message)
        : base(message)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriException"/> class.
    /// </summary>
    /// <param name="operation">The ARI operation that failed.</param>
    /// <param name="statusCode">The HTTP status code returned by Asterisk, when available.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AsteriskAriException(
        string operation,
        HttpStatusCode? statusCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the ARI operation that failed.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the HTTP status code returned by Asterisk, when available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }
}
