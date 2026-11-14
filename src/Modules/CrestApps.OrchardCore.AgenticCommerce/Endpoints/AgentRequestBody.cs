using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AgenticCommerce.Endpoints;

/// <summary>
/// Reads an agent request's body once, exactly as it was sent, so its signature and digest can be checked and the same
/// bytes parsed.
/// </summary>
public static class AgentRequestBody
{
    /// <summary>
    /// The largest body an agent may send.
    /// </summary>
    public const int MaxLength = 1024 * 1024;

    // A property named twice is refused when the body is read, as invalid JSON. Allowed, it made the first read of the
    // object throw, past every JsonException handler, and the request failed with a 500 before the caller signed in.
    private static readonly JsonDocumentOptions _documentOptions = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// Returns the body's bytes, or <see langword="null"/> when it is too large.
    /// </summary>
    /// <param name="request">The request.</param>
    public static async Task<byte[]> ReadAsync(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ContentLength > MaxLength)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;

        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxLength)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Parses a body as JSON, or returns <see langword="null"/> when it is empty or isn't JSON, a property named twice
    /// included.
    /// </summary>
    /// <param name="body">The body.</param>
    public static JsonNode Parse(byte[] body)
    {
        if (body is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body, documentOptions: _documentOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
