using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIToolsService : IAIToolsService
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICatalog<AIToolInstance> _toolInstanceStore;

    public DefaultAIToolsService(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IServiceProvider serviceProvider,
        ICatalog<AIToolInstance> aIToolInstanceStore)
    {
        _toolDefinitions = toolDefinitions.Value;
        _serviceProvider = serviceProvider;
        _toolInstanceStore = aIToolInstanceStore;
    }

    public ValueTask<AITool> GetByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return ValueTask.FromResult(_serviceProvider.GetKeyedService<AITool>(name));
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
