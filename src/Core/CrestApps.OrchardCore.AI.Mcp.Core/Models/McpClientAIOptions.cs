using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class McpClientAIOptions
{
    private readonly Dictionary<string, McpClientTypeEntry> _transportTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, McpClientTypeEntry> TransportTypes
        => _transportTypes;

    public void AddTransportType(string type, Action<McpClientTypeEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        if (!_transportTypes.TryGetValue(type, out var entry))
        {
            entry = new McpClientTypeEntry();
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(type, type);
        }

        _transportTypes[type] = entry;
    }
}

public sealed class McpClientTypeEntry
{
    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
