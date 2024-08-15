
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public abstract class SubscriptionHandlerBase : ISubscriptionHandler
{
    public virtual Task ActivatingAsync(SubscriptionFlowActivatingContext context)
        => Task.CompletedTask;

    public virtual Task ActivatedAsync(SubscriptionFlowActivatedContext context)
        => Task.CompletedTask;

    public virtual Task InitializingAsync(SubscriptionFlowInitializingContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(SubscriptionFlowInitializedContext context)
        => Task.CompletedTask;

    public virtual Task LoadingAsync(SubscriptionFlowLoadingContext context)
        => Task.CompletedTask;

    public virtual Task LoadedAsync(SubscriptionFlowLoadedContext context)
        => Task.CompletedTask;

    public virtual Task CompletingAsync(SubscriptionFlowCompletingContext context)
        => Task.CompletedTask;

    public virtual Task CompletedAsync(SubscriptionFlowCompletedContext context)
        => Task.CompletedTask;
}
