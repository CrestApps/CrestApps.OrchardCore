
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionHandler
{
    /// <summary>
    /// Triggered after a new session is created.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task InitializingAsync(SubscriptionFlowInitializingContext context);

    /// <summary>
    /// Triggered after a session is loaded.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task LoadedAsync(SubscriptionFlowLoadedContext context);

    Task CompletedAsync(SubscriptionFlowCompletedContext context);
}
