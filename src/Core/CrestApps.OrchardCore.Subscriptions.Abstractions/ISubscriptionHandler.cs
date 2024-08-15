
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionHandler
{
    /// <summary>
    /// Triggered before a new session is activated.
    /// </summary>
    /// <param name="context"></param>
    Task ActivatingAsync(SubscriptionFlowActivatingContext context);

    /// <summary>
    /// Triggered after a new session is activated.
    /// </summary>
    /// <param name="context"></param>
    Task ActivatedAsync(SubscriptionFlowActivatedContext context);

    /// <summary>
    /// Triggered before a session is initialized.
    /// </summary>
    /// <param name="context"></param>
    Task InitializingAsync(SubscriptionFlowInitializingContext context);

    /// <summary>
    /// Triggered after a session is initialized.
    /// </summary>
    /// <param name="context"></param>
    Task InitializedAsync(SubscriptionFlowInitializedContext context);

    /// <summary>
    /// Triggered before a session is loaded.
    /// </summary>
    /// <param name="context"></param>
    Task LoadingAsync(SubscriptionFlowLoadingContext context);

    /// <summary>
    /// Triggered after a session is loaded.
    /// </summary>
    /// <param name="context"></param>
    Task LoadedAsync(SubscriptionFlowLoadedContext context);

    /// <summary>
    /// Triggered before a session is completed after everything was validated.
    /// </summary>
    /// <param name="context"></param>
    Task CompletingAsync(SubscriptionFlowCompletingContext context);

    /// <summary>
    /// Triggered after a session is completed and everything was validated.
    /// </summary>
    /// <param name="context"></param>
    Task CompletedAsync(SubscriptionFlowCompletedContext context);
}
