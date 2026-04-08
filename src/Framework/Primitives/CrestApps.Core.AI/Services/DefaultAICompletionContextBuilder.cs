using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Default implementation of <see cref="IAICompletionContextBuilder"/> that runs the
/// registered <see cref="IAICompletionContextBuilderHandler"/> pipeline.
/// </summary>
public sealed class DefaultAICompletionContextBuilder : IAICompletionContextBuilder
{
    private readonly IEnumerable<IAICompletionContextBuilderHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAICompletionContextBuilder(
        IEnumerable<IAICompletionContextBuilderHandler> handlers,
        ILogger<DefaultAICompletionContextBuilder> logger)
    {
        _handlers = handlers?.Reverse() ?? [];
        _logger = logger;
    }

    public async ValueTask<AICompletionContext> BuildAsync(object resource, Action<AICompletionContext> configure = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var context = new AICompletionContext();

        var building = new AICompletionContextBuildingContext(resource, context);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.BuildingAsync(building);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in completion context building handler {Handler}.", handler.GetType().Name);
            }
        }

        configure?.Invoke(context);

        var built = new AICompletionContextBuiltContext(resource, context);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.BuiltAsync(built);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in completion context built handler {Handler}.", handler.GetType().Name);
            }
        }

        return context;
    }
}
