using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Mcp;

internal class McpJOptions
{
    internal static readonly JsonSerializerOptions SchemaSerializerOptions = new()
    {
        WriteIndented = true,
    };
}
