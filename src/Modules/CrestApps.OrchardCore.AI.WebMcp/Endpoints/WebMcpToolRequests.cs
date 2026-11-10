using System.Text.Json;
using CrestApps.Core.AI.WebMcp.Endpoints;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.WebMcp.Endpoints;

/// <summary>
/// Reads a tool call's arguments and writes its answer the way CrestApps.Core's WebMCP endpoints do, for the module's
/// own tool endpoints.
/// </summary>
internal static class WebMcpToolRequests
{
    /// <summary>
    /// Reads the JSON object in the request body as the tool's arguments, reading at most
    /// <see cref="WebMcpEndpoints.MaxRequestBodyLength"/> bytes whether or not the request declares its length.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The arguments, empty for an empty body, or <see langword="null"/> when the body is not a JSON object;
    /// and whether the body was too large to read.</returns>
    public static async Task<(IDictionary<string, object> Arguments, bool TooLarge)> ReadArgumentsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var body = await RequestBodyReader.ReadAsync(request, WebMcpEndpoints.MaxRequestBodyLength, cancellationToken);

        if (body.IsTooLarge)
        {
            return (null, true);
        }

        var arguments = new Dictionary<string, object>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(body.Body))
        {
            return (arguments, false);
        }

        try
        {
            using var document = JsonDocument.Parse(body.Body);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, false);
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.Clone();
            }

            return (arguments, false);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    /// <summary>
    /// Writes a tool's answer as an MCP tool result, or <c>404</c> when the tool is not available to the caller.
    /// </summary>
    /// <param name="result">The tool's answer.</param>
    /// <returns>The result.</returns>
    public static IResult ToResult(WebMcpToolInvocationResult result)
    {
        if (result.Status == WebMcpToolInvocationStatus.NotFound)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new WebMcpToolCallResponse
        {
            Content =
            [
                new WebMcpTextContent
                {
                    Text = result.Text,
                },
            ],
            IsError = result.IsError,
        });
    }
}
