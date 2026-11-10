using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The answers of the UCP REST binding.
/// </summary>
public static class UcpResults
{
    /// <summary>
    /// Returns a transport error: <c>{ code, content }</c> with an HTTP status.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="code">The error code.</param>
    /// <param name="content">What went wrong.</param>
    public static IResult Error(int statusCode, string code, string content)
        => Results.Json(new JsonObject
        {
            ["code"] = code,
            ["content"] = content,
        }, statusCode: statusCode);

    /// <summary>
    /// Returns a JSON answer.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="statusCode">The HTTP status.</param>
    public static IResult Json(JsonObject body, int statusCode = StatusCodes.Status200OK)
        => Results.Json(body, statusCode: statusCode);
}
