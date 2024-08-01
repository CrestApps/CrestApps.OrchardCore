
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public abstract class SubscriptionHandlerBase : ISubscriptionHandler
{
    public virtual Task InitializingAsync(SubscriptionFlowInitializationContext context)
        => Task.CompletedTask;

    public virtual Task InitializedAsync(SubscriptionFlowInitializationContext context)
        => Task.CompletedTask;

    public virtual Task CompletedAsync(SubscriptionFlowCompletedContext context)
        => Task.CompletedTask;
}
