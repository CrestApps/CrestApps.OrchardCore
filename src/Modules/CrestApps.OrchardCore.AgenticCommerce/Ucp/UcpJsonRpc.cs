using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// JSON-RPC 2.0 answers for UCP's JSON-RPC bindings, with UCP's error codes: <c>-32001</c> for discovery and version
/// failures, <c>-32000</c> for the other protocol errors (sign-in, conflicts, rate limits, not found), <c>-32600</c> for
/// a malformed request or signature, and <c>-32603</c> for a server error. The UCP error code and its text travel in
/// <c>error.data</c> as <c>{ code, content }</c>, with <c>retry_after</c> when the caller should try again.
/// </summary>
public static class UcpJsonRpc
{
    /// <summary>
    /// Invalid JSON was received.
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// The JSON sent is not a valid request.
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// The method does not exist.
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// The method's parameters are not valid.
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// A server error.
    /// </summary>
    public const int InternalError = -32603;

    /// <summary>
    /// UCP's code for a protocol error such as sign-in, an idempotency conflict, a rate limit or a missing resource.
    /// </summary>
    public const int ProtocolError = -32000;

    /// <summary>
    /// UCP's code for a discovery or version failure.
    /// </summary>
    public const int DiscoveryError = -32001;

    /// <summary>
    /// Endpoint metadata that makes <see cref="UcpRequestFilter"/> answer its errors (sign-in, profile, version,
    /// signature) as JSON-RPC errors instead of REST errors, keeping the REST binding's HTTP status.
    /// </summary>
    public static readonly UcpJsonRpcErrors Errors = new();

    /// <summary>
    /// Returns the JSON-RPC code for a UCP transport error code.
    /// </summary>
    /// <param name="ucpCode">The UCP error code, such as <c>profile_unreachable</c>.</param>
    public static int ToErrorCode(string ucpCode)
        => ucpCode switch
        {
            "invalid_profile_url" or "profile_unreachable" or "profile_malformed" or "version_unsupported" => DiscoveryError,
            "digest_mismatch" or "algorithm_unsupported" or "request_too_large" => InvalidRequest,
            UcpOperationResult.InvalidRequestCode => InvalidParams,
            "server_error" => InternalError,
            _ => ProtocolError,
        };

    /// <summary>
    /// Returns a JSON-RPC success answer.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="result">The result.</param>
    public static JsonObject Result(JsonNode id, JsonNode result)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result,
        };

    /// <summary>
    /// Returns a JSON-RPC error answer.
    /// </summary>
    /// <param name="id">The request's id, or <see langword="null"/> when it couldn't be read.</param>
    /// <param name="code">The JSON-RPC error code.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="data">The error's data, such as UCP's <c>{ code, content }</c>.</param>
    public static JsonObject Error(JsonNode id, int code, string message, JsonObject data = null)
    {
        var error = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };

        if (data is not null)
        {
            error["data"] = data;
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = error,
        };
    }

    /// <summary>
    /// Returns a JSON-RPC error answer for a UCP transport error: the UCP code mapped to its JSON-RPC code, with the UCP
    /// code and text in <c>error.data</c>.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <param name="ucpCode">The UCP error code.</param>
    /// <param name="content">What went wrong.</param>
    /// <param name="retryAfter">The seconds to wait before trying again.</param>
    public static JsonObject TransportError(JsonNode id, string ucpCode, string content, int? retryAfter = null)
    {
        var data = new JsonObject
        {
            ["code"] = ucpCode,
            ["content"] = content,
        };

        if (retryAfter is { } seconds)
        {
            data["retry_after"] = seconds;
        }

        return Error(id, ToErrorCode(ucpCode), content, data);
    }

    /// <summary>
    /// Returns the id of a JSON-RPC request's body, or <see langword="null"/>.
    /// </summary>
    /// <param name="body">The raw body.</param>
    public static JsonNode ReadId(byte[] body)
        => AgentRequestBody.Parse(body) is JsonObject request ? request["id"] : null;

    /// <summary>
    /// Returns a UCP transport error as a JSON-RPC error with the REST binding's HTTP status.
    /// </summary>
    /// <param name="body">The request's raw body, for its id.</param>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="code">The UCP error code.</param>
    /// <param name="content">What went wrong.</param>
    public static IResult WriteTransportError(byte[] body, int statusCode, string code, string content)
        => Results.Json(TransportError(ReadId(body), code, content), statusCode: statusCode);
}

/// <summary>
/// The endpoint metadata <see cref="UcpJsonRpc.Errors"/>: the endpoint is a JSON-RPC binding.
/// </summary>
public sealed class UcpJsonRpcErrors
{
}
