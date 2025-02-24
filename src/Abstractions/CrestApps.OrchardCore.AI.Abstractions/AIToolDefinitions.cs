using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

public class AIToolDefinitions
{
    private readonly Dictionary<string, AIToolDefinitionEntry> _tools = [];

    public IReadOnlyDictionary<string, AIToolDefinitionEntry> Tools => _tools;

    internal void Add<TTool>(string name, Action<AIToolDefinitionEntry> configure = null)
        where TTool : AITool
    {
        if (!_tools.TryGetValue(name, out var definition))
        {
            definition = new AIToolDefinitionEntry(typeof(TTool));
        }

        if (configure != null)
        {
            configure(definition);
        }

        _tools[name] = definition;
    }
}
