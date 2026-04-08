using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Default implementation of <see cref="IOrchestrationContextBuilder"/> that runs the
/// registered <see cref="IOrchestrationContextBuilderHandler"/> pipeline.
/// </summary>
public sealed class DefaultOrchestrationContextBuilder : IOrchestrationContextBuilder
{
    private readonly IEnumerable<IOrchestrationContextBuilderHandler> _handlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DefaultOrchestrationContextBuilder(
        IEnumerable<IOrchestrationContextBuilderHandler> handlers,
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

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.BuildingAsync(building);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in orchestration context building handler {Handler}.", handler.GetType().Name);
            }
        }

        configure?.Invoke(context);

        var built = new OrchestrationContextBuiltContext(resource, context);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.BuiltAsync(built);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in orchestration context built handler {Handler}.", handler.GetType().Name);
            }
        }

        // Flush accumulated system message.
        if (context.CompletionContext != null && context.SystemMessageBuilder.Length > 0)
        {
            context.CompletionContext.SystemMessage = context.SystemMessageBuilder.ToString();
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var systemMessage = context.CompletionContext?.SystemMessage;

            if (!string.IsNullOrEmpty(systemMessage))
            {
                _logger.LogDebug("Composed system message ({Length} chars) for resource type '{ResourceType}': {SystemMessage}",
                systemMessage.Length, resource.GetType().Name, systemMessage);
            }
            else
            {
                _logger.LogDebug("No system message composed for resource type '{ResourceType}'.", resource.GetType().Name);
            }
        }

        return context;
    }
}
