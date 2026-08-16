using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// A convenience base class for <see cref="ISubscriptionHandler"/> implementations. Every method is a
/// no-op by default so a handler can override only the subscription flow events it cares about.
/// </summary>
public abstract class SubscriptionHandlerBase : ISubscriptionHandler
{
    /// <inheritdoc/>
    public virtual Task ActivatingAsync(SubscriptionFlowActivatingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ActivatedAsync(SubscriptionFlowActivatedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializingAsync(SubscriptionFlowInitializingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializedAsync(SubscriptionFlowInitializedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadingAsync(SubscriptionFlowLoadingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadedAsync(SubscriptionFlowLoadedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletingAsync(SubscriptionFlowCompletingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletedAsync(SubscriptionFlowCompletedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task FailedAsync(SubscriptionFlowFailedContext context)
        => Task.CompletedTask;
}
