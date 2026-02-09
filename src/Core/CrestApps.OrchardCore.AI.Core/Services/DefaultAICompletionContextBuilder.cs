using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

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
        await _handlers.InvokeAsync((h, c) => h.BuildingAsync(c), building, _logger);

        // Allow caller override last.
        configure?.Invoke(context);

        var built = new AICompletionContextBuiltContext(resource, context);
        await _handlers.InvokeAsync((h, c) => h.BuiltAsync(c), built, _logger);

        return context;
    }
}
