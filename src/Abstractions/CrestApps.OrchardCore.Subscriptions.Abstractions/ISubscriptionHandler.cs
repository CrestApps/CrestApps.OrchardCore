using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Handles the lifecycle events of a subscription checkout flow so features can contribute steps,
/// validate state, and react to completion or failure.
/// </summary>
public interface ISubscriptionHandler
{
    /// <summary>
    /// Triggered before a new session is activated.
    /// </summary>
    /// <param name="context">The context for the activating session.</param>
    Task ActivatingAsync(SubscriptionFlowActivatingContext context);

    /// <summary>
    /// Triggered after a new session is activated.
    /// </summary>
    /// <param name="context">The context for the activated session.</param>
    Task ActivatedAsync(SubscriptionFlowActivatedContext context);

    /// <summary>
    /// Triggered before a session is initialized.
    /// </summary>
    /// <param name="context">The context for the initializing session.</param>
    Task InitializingAsync(SubscriptionFlowInitializingContext context);

    /// <summary>
    /// Triggered after a session is initialized.
    /// </summary>
    /// <param name="context">The context for the initialized session.</param>
    Task InitializedAsync(SubscriptionFlowInitializedContext context);

    /// <summary>
    /// Triggered before a session is loaded.
    /// </summary>
    /// <param name="context">The context for the loading session.</param>
    Task LoadingAsync(SubscriptionFlowLoadingContext context);

    /// <summary>
    /// Triggered after a session is loaded.
    /// </summary>
    /// <param name="context">The context for the loaded session.</param>
    Task LoadedAsync(SubscriptionFlowLoadedContext context);

    /// <summary>
    /// Triggered before a session is completed, after everything was validated.
    /// </summary>
    /// <param name="context">The context for the completing session.</param>
    Task CompletingAsync(SubscriptionFlowCompletingContext context);

    /// <summary>
    /// Triggered after a session is completed and everything was validated.
    /// </summary>
    /// <param name="context">The context for the completed session.</param>
    Task CompletedAsync(SubscriptionFlowCompletedContext context);

    /// <summary>
    /// Triggered only when a session fails.
    /// </summary>
    /// <param name="context">The context for the failed session.</param>
    Task FailedAsync(SubscriptionFlowFailedContext context);
}
