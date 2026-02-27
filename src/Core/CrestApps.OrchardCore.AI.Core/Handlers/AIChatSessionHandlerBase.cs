using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Base class for <see cref="IAIChatSessionHandler"/> implementations.
/// Provides virtual no-op implementations for all lifecycle events
/// inherited from <see cref="ICatalogEntryHandler{AIChatSession}"/>
/// and <see cref="IAIChatSessionHandler.MessageCompletedAsync"/>.
/// </summary>
public abstract class AIChatSessionHandlerBase : CatalogEntryHandlerBase<AIChatSession>, IAIChatSessionHandler
{
    /// <inheritdoc/>
    public virtual Task MessageCompletedAsync(ChatMessageCompletedContext context)
        => Task.CompletedTask;
}
