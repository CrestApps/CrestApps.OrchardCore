using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// The answer of an ACP checkout operation, in the shape of the REST binding's: an HTTP status and a body, which is the
/// checkout session on success or ACP's flat error. The MCP binding turns it into a tool result or a JSON-RPC error.
/// </summary>
public sealed class AcpOperationResult
{
    /// <summary>
    /// Gets the HTTP status.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the body: the checkout session, or the error.
    /// </summary>
    public JsonObject Body { get; init; }

    /// <summary>
    /// Gets the seconds to wait before trying again, or <see langword="null"/>.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded
        => StatusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices;

    /// <summary>
    /// Returns a successful answer.
    /// </summary>
    /// <param name="body">The checkout session.</param>
    /// <param name="statusCode">The HTTP status.</param>
    public static AcpOperationResult Success(JsonObject body, int statusCode = StatusCodes.Status200OK)
        => new()
        {
            StatusCode = statusCode,
            Body = body,
        };

    /// <summary>
    /// Returns an error answer.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="code">The error's code.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="param">The JSON path of the field it is about, or <see langword="null"/>.</param>
    /// <param name="retryAfterSeconds">The seconds to wait before trying again, or <see langword="null"/>.</param>
    public static AcpOperationResult Error(int statusCode, string code, string message, string param = null, int? retryAfterSeconds = null)
        => new()
        {
            StatusCode = statusCode,
            Body = AcpResults.ErrorBody(statusCode, code, message, param),
            RetryAfterSeconds = retryAfterSeconds,
        };
}
