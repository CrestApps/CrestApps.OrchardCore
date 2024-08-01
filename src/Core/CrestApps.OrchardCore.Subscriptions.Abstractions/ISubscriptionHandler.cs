
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionHandler
{
    Task InitializingAsync(SubscriptionFlowInitializationContext context);

    Task InitializedAsync(SubscriptionFlowInitializationContext context);

    Task CompletedAsync(SubscriptionFlowCompletedContext context);
}
