using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultOrchestrationContextBuilder : IOrchestrationContextBuilder
{
    private readonly IEnumerable<IOrchestrationContextHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultOrchestrationContextBuilder(
        IEnumerable<IOrchestrationContextHandler> handlers,
        ILogger<DefaultOrchestrationContextBuilder> logger)
    {
        _handlers = handlers?.Reverse() ?? [];
        _logger = logger;
    }

    public async ValueTask<OrchestrationContext> BuildAsync(object resource, Action<OrchestrationContext> configure = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var context = new OrchestrationContext();

        var building = new OrchestrationContextBuildingContext(resource, context);
        await _handlers.InvokeAsync((h, c) => h.BuildingAsync(c), building, _logger);

        // Allow caller override last.
        configure?.Invoke(context);

        var built = new OrchestrationContextBuiltContext(resource, context);
        await _handlers.InvokeAsync((h, c) => h.BuiltAsync(c), built, _logger);

        return context;
    }
}
