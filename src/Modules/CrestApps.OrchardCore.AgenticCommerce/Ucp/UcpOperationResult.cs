using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The answer to a UCP operation, before a binding writes it: a UCP body (a checkout, an order, or an error envelope
/// for a business outcome), or a transport error such as not found, busy or too many requests.
/// </summary>
public sealed class UcpOperationResult
{
    /// <summary>
    /// The transport error code of a busy checkout.
    /// </summary>
    public const string ConflictCode = "conflict";

    /// <summary>
    /// The transport error code of a missing checkout or order.
    /// </summary>
    public const string NotFoundCode = "not_found";

    /// <summary>
    /// The transport error code of a malformed request.
    /// </summary>
    public const string InvalidRequestCode = "invalid_request";

    /// <summary>
    /// The transport error code of too many requests.
    /// </summary>
    public const string RateLimitedCode = "rate_limited";

    /// <summary>
    /// Gets the HTTP status the REST binding answers with.
    /// </summary>
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    /// <summary>
    /// Gets the UCP body, or <see langword="null"/> for a transport error.
    /// </summary>
    public JsonObject Body { get; init; }

    /// <summary>
    /// Gets the transport error code, such as <c>not_found</c>.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    /// Gets what went wrong, for a transport error.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets the seconds to wait before trying again, for a busy checkout.
    /// </summary>
    public int? RetryAfter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the answer is a transport error rather than a UCP body.
    /// </summary>
    public bool IsTransportError
        => Body is null;

    /// <summary>
    /// Gets a value indicating whether the answer is final, and kept for a retry with the same idempotency key. A busy
    /// checkout, too many requests and a server error are worth trying again.
    /// </summary>
    public bool IsFinal
        => StatusCode < StatusCodes.Status500InternalServerError &&
            StatusCode != StatusCodes.Status429TooManyRequests &&
            ErrorCode != ConflictCode;

    /// <summary>
    /// Returns a UCP body.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="statusCode">The HTTP status of the REST binding.</param>
    public static UcpOperationResult Answer(JsonObject body, int statusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new UcpOperationResult { Body = body, StatusCode = statusCode };
    }

    /// <summary>
    /// Returns a transport error.
    /// </summary>
    /// <param name="statusCode">The HTTP status of the REST binding.</param>
    /// <param name="code">The error code.</param>
    /// <param name="error">What went wrong.</param>
    /// <param name="retryAfter">The seconds to wait before trying again.</param>
    public static UcpOperationResult Fail(int statusCode, string code, string error, int? retryAfter = null)
        => new() { StatusCode = statusCode, ErrorCode = code, Error = error, RetryAfter = retryAfter };

    /// <summary>
    /// Returns the answer of the REST binding.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public IResult ToHttpResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!IsTransportError)
        {
            return UcpResults.Json(Body, StatusCode);
        }

        if (RetryAfter is { } seconds)
        {
            httpContext.Response.Headers[HeaderNames.RetryAfter] = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return UcpResults.Error(StatusCode, ErrorCode, Error);
    }
}
