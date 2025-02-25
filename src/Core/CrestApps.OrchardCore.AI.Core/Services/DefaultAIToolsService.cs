using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIToolsService : IAIToolsService
{
    private readonly AIToolDefinitions _toolDefinitions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIToolInstanceStore _toolInstanceStore;

    public DefaultAIToolsService(
        AIToolDefinitions toolDefinitions,
        IServiceProvider serviceProvider,
        IAIToolInstanceStore aIToolInstanceStore)
    {
        _toolDefinitions = toolDefinitions;
        _serviceProvider = serviceProvider;
        _toolInstanceStore = aIToolInstanceStore;
    }

    public ValueTask<AITool> GetByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_toolDefinitions.Tools.TryGetValue(name, out var definition))
        {
            return ValueTask.FromResult(ActivatorUtilities.CreateInstance(_serviceProvider, definition.ToolType) as AITool);
        }

        return ValueTask.FromResult<AITool>(null);
    }

    public async ValueTask<AITool> GetByInstanceIdAsync(string id)
    {
        var instance = await _toolInstanceStore.FindByIdAsync(id);

        if (instance is not null)
        {
            var source = _serviceProvider.GetKeyedService<IAIToolSource>(instance.Source);

            if (source != null)
            {
                return await source.CreateAsync(instance);
            }
        }

        return null;
    }

    public async ValueTask<AITool> GetAsync(string instanceIdOrToolName)
    {
        var tool = await GetByInstanceIdAsync(instanceIdOrToolName);

        return tool ?? await GetByNameAsync(instanceIdOrToolName);
    }
}
