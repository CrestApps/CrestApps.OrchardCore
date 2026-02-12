using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public sealed class AIToolDefinitionOptions
{
    private readonly Dictionary<string, AIToolDefinitionEntry> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AIToolDefinitionEntry> Tools => _tools;

    internal void SetTool(string name, AIToolDefinitionEntry entry)
    {
        _tools[name] = entry;
    }
}
