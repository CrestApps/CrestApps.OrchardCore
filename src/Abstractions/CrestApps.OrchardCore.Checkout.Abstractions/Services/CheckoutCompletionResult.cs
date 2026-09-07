namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The outcome of asking the engine to complete a checkout.
/// </summary>
public sealed class CheckoutCompletionResult
{
    /// <summary>
    /// Gets or sets the outcome.
    /// </summary>
    public CheckoutCompletionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the key of the step the customer must return to when the checkout cannot complete because
    /// an earlier step was never filled in.
    /// </summary>
    public string BlockingStepKey { get; set; }

    /// <summary>
    /// Gets or sets the reason the checkout failed, suitable for showing to the customer.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets the obligations that are still waiting on the provider, when the status is
    /// <see cref="CheckoutCompletionStatus.Pending"/>.
    /// </summary>
    public IList<string> OutstandingObligationIds { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the checkout is finished and fulfillment has run.
    /// </summary>
    public bool IsCompleted
        => Status is CheckoutCompletionStatus.Completed or CheckoutCompletionStatus.AlreadyCompleted;
}

/// <summary>
/// The distinct outcomes of an attempt to complete a checkout. They are separate values because a caller
/// reacts differently to each: a pending checkout should be polled, a blocked one should send the customer
/// back to a step, and a failed one should show an error — collapsing them into a boolean would leave a
/// customer staring at "something went wrong" while their payment was merely still processing.
/// </summary>
public enum CheckoutCompletionStatus
{
    /// <summary>
    /// Every obligation settled and the completion handlers ran.
    /// </summary>
    Completed,

    /// <summary>
    /// The checkout had already completed. Repeating the call is safe and changes nothing.
    /// </summary>
    AlreadyCompleted,

    /// <summary>
    /// One or more obligations are still awaiting an authoritative answer from the provider. The caller
    /// should try again rather than treat this as a failure.
    /// </summary>
    Pending,

    /// <summary>
    /// A step that collects data has not been completed, so the checkout cannot be finalized yet.
    /// </summary>
    Blocked,

    /// <summary>
    /// The checkout failed: an obligation failed at the provider, or a completion handler threw.
    /// </summary>
    Failed,

    /// <summary>
    /// The checkout was canceled.
    /// </summary>
    Canceled,

    /// <summary>
    /// No checkout with the supplied id exists.
    /// </summary>
    NotFound,
}
