using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultAIToolsService : IAIToolsService
{
    private readonly IEnumerable<AITool> _tools;

    private Dictionary<string, AIFunction> _functions;

    public DefaultAIToolsService(IEnumerable<AITool> tools)
    {
        _tools = tools;
    }

    public IEnumerable<AIFunction> GetFunctions()
    {
        LoadFunctions();

        return _functions.Values;
    }

    public IEnumerable<AITool> GetTools()
        => _tools;

    public AIFunction GetFunction(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        LoadFunctions();

        return _functions.TryGetValue(name, out var function)
            ? function
            : null;
    }

    private void LoadFunctions()
    {
        if (_functions is not null)
        {
            return;
        }

        _functions = _tools.Where(x => x is AIFunction).Cast<AIFunction>().ToDictionary(x => x.Metadata.Name);
    }
}
