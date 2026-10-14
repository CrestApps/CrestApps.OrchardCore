namespace CrestApps.OrchardCore.Wizard.Handlers;

/// <summary>
/// Handles the lifecycle of a wizard flow. Features implement this to contribute steps, guard step
/// transitions, and react to completion or failure. Implementations should early-return for wizard types
/// they do not own by inspecting <see cref="WizardSession.WizardType"/>.
/// </summary>
public interface IWizardHandler
{
    /// <summary>
    /// Triggered before a new session is activated. Add steps here.
    /// </summary>
    /// <param name="context">The activating context.</param>
    Task ActivatingAsync(WizardFlowActivatingContext context);

    /// <summary>
    /// Triggered after a new session is activated.
    /// </summary>
    /// <param name="context">The activated context.</param>
    Task ActivatedAsync(WizardFlowActivatedContext context);

    /// <summary>
    /// Triggered before a session is initialized for display.
    /// </summary>
    /// <param name="context">The initializing context.</param>
    Task InitializingAsync(WizardFlowInitializingContext context);

    /// <summary>
    /// Triggered after a session is initialized for display.
    /// </summary>
    /// <param name="context">The initialized context.</param>
    Task InitializedAsync(WizardFlowInitializedContext context);

    /// <summary>
    /// Triggered before a session is loaded for the current step.
    /// </summary>
    /// <param name="context">The loading context.</param>
    Task LoadingAsync(WizardFlowLoadingContext context);

    /// <summary>
    /// Triggered after a session is loaded for the current step.
    /// </summary>
    /// <param name="context">The loaded context.</param>
    Task LoadedAsync(WizardFlowLoadedContext context);

    /// <summary>
    /// Triggered before a session is completed, after every step has been validated.
    /// </summary>
    /// <param name="context">The completing context.</param>
    Task CompletingAsync(WizardFlowCompletingContext context);

    /// <summary>
    /// Triggered after a session has completed and every step has been fulfilled.
    /// </summary>
    /// <param name="context">The completed context.</param>
    Task CompletedAsync(WizardFlowCompletedContext context);

    /// <summary>
    /// Triggered when a session fails.
    /// </summary>
    /// <param name="context">The failed context.</param>
    Task FailedAsync(WizardFlowFailedContext context);
}
