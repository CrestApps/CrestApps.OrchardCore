using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIToolsService : IAIToolsService
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultAIToolsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ValueTask<AITool> GetByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return ValueTask.FromResult(_serviceProvider.GetKeyedService<AITool>(name));
    }
}
