using System.Text.Json;
using CrestApps.Core.AI.WebMcp.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.WebMcp.Endpoints;

/// <summary>
/// Reads a tool call's arguments and writes its answer the way CrestApps.Core's WebMCP endpoints do, for the module's
/// own tool endpoints.
/// </summary>
internal static class WebMcpToolRequests
{
    /// <summary>
    /// Reads the JSON object in the request body as the tool's arguments.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The arguments, empty for an empty body, or <see langword="null"/> when the body is not a JSON object.</returns>
    public static async Task<IDictionary<string, object>> ReadArgumentsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal);

        if (request.ContentLength == 0)
        {
            return arguments;
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.Clone();
            }

            return arguments;
        }
        catch (JsonException)
        {
            return null;
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
