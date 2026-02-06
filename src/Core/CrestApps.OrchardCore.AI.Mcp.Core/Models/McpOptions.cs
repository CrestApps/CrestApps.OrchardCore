using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class McpOptions
{
    private readonly Dictionary<string, McpResourceTypeEntry> _resourceTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, McpResourceTypeEntry> ResourceTypes
        => _resourceTypes;

    public void AddResourceType(string type, Action<McpResourceTypeEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        if (!_resourceTypes.TryGetValue(type, out var entry))
        {
            entry = new McpResourceTypeEntry(type);
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(type, type);
        }

        _resourceTypes[type] = entry;
    }
}

public sealed class McpResourceTypeEntry
{
    public McpResourceTypeEntry(string type)
    {
        Type = type;
    }

    public string Type { get; private set; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }

    /// <summary>
    /// Gets or sets the path patterns supported by this resource type.
    /// These are displayed in the UI to help users understand what path formats are valid.
    /// The full URI is constructed as {type}://{itemId}/{path} where {type} is the resource type
    /// and {itemId} is the system-generated identifier.
    /// Example: ["{path}", "id/{contentItemId}", "{contentType}/list"]
    /// </summary>
    public string[] UriPatterns { get; set; } = [];
}
