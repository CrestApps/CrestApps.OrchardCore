namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Builds and reads the address of an MCP server resource: the resource type as the scheme, the resource's ID, and the
/// path a person enters, such as <c>file://4f2a/{providerName}/{fileName}</c>. Shared by the resource editor, the
/// resource handler and the AI tools.
/// </summary>
public static class McpResourceUris
{
    /// <summary>
    /// Builds a resource's address from the path a person enters.
    /// </summary>
    /// <param name="source">The resource type.</param>
    /// <param name="itemId">The ID of the resource.</param>
    /// <param name="path">The path, with or without a leading slash, or empty for the resource itself.</param>
    /// <returns>The address.</returns>
    public static string Build(string source, string itemId, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return $"{source}://{itemId}";
        }

        // Strip any leading slash from user path.
        path = path.TrimStart('/');

        return $"{source}://{itemId}/{path}";
    }

    /// <summary>
    /// Gets the path a person entered from a resource's address.
    /// </summary>
    /// <param name="uri">The address.</param>
    /// <param name="source">The resource type.</param>
    /// <param name="itemId">The ID of the resource.</param>
    /// <returns>The path, empty for the resource itself, or the address as it is when it is not one of this resource.</returns>
    public static string ExtractPath(string uri, string source, string itemId)
    {
        var prefix = $"{source}://{itemId}";

        if (string.IsNullOrEmpty(uri) || !uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var remainder = uri[prefix.Length..];

        if (remainder.Length == 0)
        {
            return string.Empty;
        }

        // Strip the leading slash.
        return remainder.TrimStart('/');
    }
}
