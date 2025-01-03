using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultOpenAIFunctionService : IOpenAIFunctionService
{
    private readonly IEnumerable<IOpenAIChatFunction> _functions;

    public DefaultOpenAIFunctionService(IEnumerable<IOpenAIChatFunction> functions)
    {
        _functions = functions;
    }

    private readonly object _lock = new();

    private Dictionary<string, IOpenAIChatFunction> _functionsCache;

    public IOpenAIChatFunction FindByName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_functionsCache == null)
        {
            LoadFunctions();
        }

        if (_functionsCache.TryGetValue(name, out var function))
        {
            return function;
        }

        return null;
    }

    public IEnumerable<IOpenAIChatFunction> GetFunctions()
    {
        if (_functionsCache == null)
        {
            LoadFunctions();
        }

        return _functionsCache.Values;
    }

    private void LoadFunctions()
    {

        if (_functionsCache != null)
        {
            return;
        }

        lock (_lock)
        {
            if (_functionsCache == null)
            {
                _functionsCache = [];

                foreach (var function in _functions)
                {
                    _functionsCache[function.Name] = function;
                }
            }
        }
    }
}
