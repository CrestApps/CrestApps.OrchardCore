using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultOrchestrationContextBuilder : IOrchestrationContextBuilder
{
    private readonly IEnumerable<IOrchestrationContextHandler> _handlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DefaultOrchestrationContextBuilder(
        IEnumerable<IOrchestrationContextHandler> handlers,
        IServiceProvider serviceProvider,
        ILogger<DefaultOrchestrationContextBuilder> logger)
    {
        _handlers = handlers?.Reverse() ?? [];
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask<OrchestrationContext> BuildAsync(object resource, Action<OrchestrationContext> configure = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var context = new OrchestrationContext
        {
            ServiceProvider = _serviceProvider,
        };

        var building = new OrchestrationContextBuildingContext(resource, context);
        await _handlers.InvokeAsync((h, c) => h.BuildingAsync(c), building, _logger);

        // Allow caller override last.
        configure?.Invoke(context);

        var built = new OrchestrationContextBuiltContext(resource, context);
        await _handlers.InvokeAsync((h, c) => h.BuiltAsync(c), built, _logger);

        // Flush accumulated system message.
        if (context.CompletionContext != null && context.SystemMessageBuilder.Length > 0)
        {
            context.CompletionContext.SystemMessage = context.SystemMessageBuilder.ToString();
        }

        return context;
    }
}
