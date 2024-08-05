
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionHandler
{
    /// <summary>
    /// Triggered before a new session is initialized.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task InitializingAsync(SubscriptionFlowInitializingContext context);

    /// <summary>
    /// Triggered after a new session is initialized.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task InitializedAsync(SubscriptionFlowInitializedContext context);

    /// <summary>
    /// Triggered after a session is loaded.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task LoadedAsync(SubscriptionFlowLoadedContext context);

    /// <summary>
    /// Triggered before a session is completed after everything was validated.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task CompletingAsync(SubscriptionFlowCompletedContext context);

    /// <summary>
    /// Triggered after a session is completed and everything was validated.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task CompletedAsync(SubscriptionFlowCompletedContext context);
}
