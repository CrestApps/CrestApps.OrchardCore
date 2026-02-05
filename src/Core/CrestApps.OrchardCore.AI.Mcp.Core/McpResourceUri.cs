namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Represents a parsed MCP resource URI with the format {scheme}://{itemId}/{path}.
/// Provides centralized parsing, building, and path extraction for resource URIs.
/// </summary>
public sealed class McpResourceUri
{
    /// <summary>
    /// Gets the URI scheme, which corresponds to the resource type (e.g., "file", "content", "ftp").
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets the system-generated item identifier extracted from the URI host.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the path portion of the URI (everything after {scheme}://{itemId}/).
    /// </summary>
    public string Path { get; }

    // Gets the full URI string in the format {scheme}://{itemId}/{path}.
    public string Uri => $"{Scheme}://{ItemId}/{Path}";

    public override string ToString()
    {
        return Uri;
    }

    private McpResourceUri(string scheme, string itemId, string path)
    {
        Scheme = scheme;
        ItemId = itemId;
        Path = path;
    }

    /// <summary>
    /// Attempts to parse a full resource URI into its components.
    /// </summary>
    /// <param name="uri">The URI string to parse.</param>
    /// <param name="result">When successful, the parsed <see cref="McpResourceUri"/>.</param>
    /// <returns><c>true</c> if the URI was parsed successfully; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string uri, out McpResourceUri result)
    {
        result = null;

        if (string.IsNullOrEmpty(uri) || !System.Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        var scheme = parsedUri.Scheme;
        var itemId = parsedUri.Host;

        // Use UnescapeDataString to preserve characters like { and } that System.Uri encodes.
        var path = System.Uri.UnescapeDataString(parsedUri.AbsolutePath).TrimStart('/');

        if (string.IsNullOrEmpty(scheme) || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        result = new McpResourceUri(scheme, itemId, path);

        return true;
    }

    /// <summary>
    /// Constructs a full resource URI from its components.
    /// </summary>
    /// <param name="scheme">The URI scheme (resource type).</param>
    /// <param name="itemId">The system-generated item identifier.</param>
    /// <param name="path">The path portion of the URI.</param>
    /// <returns>The constructed URI string, or <see cref="string.Empty"/> if the path is empty.</returns>
    public static string Build(string scheme, string itemId, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        var trimmedPath = path?.TrimStart('/') ?? string.Empty;

        return string.IsNullOrEmpty(trimmedPath)
            ? string.Empty
            : $"{scheme}://{itemId}/{trimmedPath}";
    }
}
