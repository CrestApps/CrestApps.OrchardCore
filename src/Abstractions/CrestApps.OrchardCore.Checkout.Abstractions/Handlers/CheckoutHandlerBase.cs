namespace CrestApps.OrchardCore.Checkout.Handlers;

/// <summary>
/// A convenience base class for <see cref="ICheckoutHandler"/> implementations that lets handlers
/// override only the lifecycle events they care about.
/// </summary>
public abstract class CheckoutHandlerBase : ICheckoutHandler
{
    /// <inheritdoc/>
    public virtual Task ActivatingAsync(CheckoutFlowActivatingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ActivatedAsync(CheckoutFlowActivatedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializingAsync(CheckoutFlowInitializingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task InitializedAsync(CheckoutFlowInitializedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadingAsync(CheckoutFlowLoadingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task LoadedAsync(CheckoutFlowLoadedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletingAsync(CheckoutFlowCompletingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CompletedAsync(CheckoutFlowCompletedContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task FailedAsync(CheckoutFlowFailedContext context)
        => Task.CompletedTask;
}
