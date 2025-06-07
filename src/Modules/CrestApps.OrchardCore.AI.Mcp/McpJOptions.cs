using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Mcp;

internal static class McpJOptions
{
    internal static readonly JsonSerializerOptions SchemaSerializerOptions = new()
    {
        WriteIndented = true,
    };
}
