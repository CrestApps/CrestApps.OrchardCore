using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public sealed class AIToolDefinitions
{
    private readonly Dictionary<string, AIToolDefinitionEntry> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, AIToolDefinitionEntry> Tools => _tools;

    internal void Add<TTool>(string name, Action<AIToolDefinitionEntry> configure = null)
        where TTool : AITool
    {
        if (!_tools.TryGetValue(name, out var entry))
        {
            entry = new AIToolDefinitionEntry(typeof(TTool));
        }

        if (configure != null)
        {
            configure(entry);
        }

        _tools[name] = entry;
    }
}
