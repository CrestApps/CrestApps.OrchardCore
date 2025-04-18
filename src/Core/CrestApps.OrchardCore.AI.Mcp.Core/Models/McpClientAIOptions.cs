using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class McpClientAIOptions
{
    private readonly Dictionary<string, McpClientTransportEntry> _transportTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, McpClientTransportEntry> TransportTypes
        => _transportTypes;

    public void AddTransportType(string type, Action<McpClientTransportEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        if (!_transportTypes.TryGetValue(type, out var entry))
        {
            entry = new McpClientTransportEntry(type);
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

public sealed class McpClientTransportEntry
{
    public McpClientTransportEntry(string type)
    {
        Type = type;
    }

    public string Type { get; private set; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
