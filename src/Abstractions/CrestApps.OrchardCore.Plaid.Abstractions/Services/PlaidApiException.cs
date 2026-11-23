using System.Net;
using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Plaid answered a call with an error. The message holds Plaid's codes and request id, never a token or a number.
/// </summary>
public sealed class PlaidApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="error">The error Plaid answered with, or <see langword="null"/> when the answer could not be read.</param>
    public PlaidApiException(HttpStatusCode statusCode, PlaidError error)
        : base($"Plaid answered {(int)statusCode}: {error?.ErrorType ?? "UNKNOWN"} {error?.ErrorCode ?? "UNKNOWN"} (request {error?.RequestId ?? "none"}).")
    {
        StatusCode = statusCode;
        Error = error ?? new PlaidError();
    }

    /// <summary>
    /// Gets the HTTP status.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the error.
    /// </summary>
    public PlaidError Error { get; }

    /// <summary>
    /// Gets the error code, such as <c>PRODUCT_NOT_READY</c>.
    /// </summary>
    public string ErrorCode
        => Error.ErrorCode;
}
