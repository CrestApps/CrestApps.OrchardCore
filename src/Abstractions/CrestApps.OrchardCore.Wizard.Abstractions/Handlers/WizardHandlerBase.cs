namespace CrestApps.OrchardCore.Wizard.Handlers;

/// <summary>
/// A convenience base class for <see cref="IWizardHandler"/> implementations that lets handlers override
/// only the lifecycle events they care about.
/// </summary>
public abstract class WizardHandlerBase : IWizardHandler
{
    /// <inheritdoc/>
    public virtual Task ActivatingAsync(WizardFlowActivatingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ActivatedAsync(WizardFlowActivatedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializingAsync(WizardFlowInitializingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializedAsync(WizardFlowInitializedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadingAsync(WizardFlowLoadingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadedAsync(WizardFlowLoadedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletingAsync(WizardFlowCompletingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletedAsync(WizardFlowCompletedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task FailedAsync(WizardFlowFailedContext context)
        => Task.CompletedTask;
}
