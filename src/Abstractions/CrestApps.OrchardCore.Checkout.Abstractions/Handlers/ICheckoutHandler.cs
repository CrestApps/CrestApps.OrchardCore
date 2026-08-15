namespace CrestApps.OrchardCore.Checkout.Handlers;

/// <summary>
/// Handles the lifecycle of a checkout flow. Features implement this to contribute steps, build the
/// invoice, guard step transitions, and react to completion or failure.
/// </summary>
public interface ICheckoutHandler
{
    /// <summary>
    /// Triggered before a new session is activated. Add steps and billing items here.
    /// </summary>
    /// <param name="context">The activating context.</param>
    Task ActivatingAsync(CheckoutFlowActivatingContext context);

    /// <summary>
    /// Triggered after a new session is activated. The invoice is built here.
    /// </summary>
    /// <param name="context">The activated context.</param>
    Task ActivatedAsync(CheckoutFlowActivatedContext context);

    /// <summary>
    /// Triggered before a session is initialized for display.
    /// </summary>
    /// <param name="context">The initializing context.</param>
    Task InitializingAsync(CheckoutFlowInitializingContext context);

    /// <summary>
    /// Triggered after a session is initialized for display.
    /// </summary>
    /// <param name="context">The initialized context.</param>
    Task InitializedAsync(CheckoutFlowInitializedContext context);

    /// <summary>
    /// Triggered before a session is loaded for the current step.
    /// </summary>
    /// <param name="context">The loading context.</param>
    Task LoadingAsync(CheckoutFlowLoadingContext context);

    /// <summary>
    /// Triggered after a session is loaded for the current step.
    /// </summary>
    /// <param name="context">The loaded context.</param>
    Task LoadedAsync(CheckoutFlowLoadedContext context);

    /// <summary>
    /// Triggered before a session is completed, after everything has been validated.
    /// </summary>
    /// <param name="context">The completing context.</param>
    Task CompletingAsync(CheckoutFlowCompletingContext context);

    /// <summary>
    /// Triggered after a session has completed and everything has been validated.
    /// </summary>
    /// <param name="context">The completed context.</param>
    Task CompletedAsync(CheckoutFlowCompletedContext context);

    /// <summary>
    /// Triggered when a session fails.
    /// </summary>
    /// <param name="context">The failed context.</param>
    Task FailedAsync(CheckoutFlowFailedContext context);
}
