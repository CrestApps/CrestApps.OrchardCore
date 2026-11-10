using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// The answers of the ACP REST binding.
/// </summary>
public static class AcpResults
{
    /// <summary>
    /// Returns an HTTP error: ACP's flat <c>{ type, code, message }</c>.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="code">The error's code.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="param">The JSON path of the field it is about, or <see langword="null"/>.</param>
    public static IResult Error(int statusCode, string code, string message, string param = null)
    {
        var json = new JsonObject
        {
            ["type"] = statusCode >= StatusCodes.Status500InternalServerError ? "processing_error" : "invalid_request",
            ["code"] = code,
            ["message"] = message,
        };

        if (!string.IsNullOrEmpty(param))
        {
            json["param"] = param;
        }

        return Results.Json(json, statusCode: statusCode);
    }

    /// <summary>
    /// Returns a JSON answer.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="statusCode">The HTTP status.</param>
    public static IResult Json(JsonObject body, int statusCode = StatusCodes.Status200OK)
        => Results.Json(body, statusCode: statusCode);
}
