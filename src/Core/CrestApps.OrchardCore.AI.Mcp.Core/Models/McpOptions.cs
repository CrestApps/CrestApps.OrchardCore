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
    /// Gets or sets the variables supported by this resource type.
    /// These are displayed in the UI to help users understand what variables can be used in URI patterns.
    /// </summary>
    public McpResourceVariable[] SupportedVariables { get; set; } = [];
}

/// <summary>
/// Describes a variable that a resource type handler supports.
/// Users can include these variables (wrapped in braces) in their URI patterns.
/// </summary>
public sealed class McpResourceVariable
{
    public McpResourceVariable(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }

    /// <summary>
    /// Gets the variable name (e.g., "path", "stepName").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a description of the variable displayed in the UI.
    /// </summary>
    public LocalizedString Description { get; set; }
}
