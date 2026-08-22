namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides no-op implementations of payment event handlers for derived classes that only handle selected events.
/// </summary>
public abstract class PaymentEventBase : IPaymentEvent
{
    /// <summary>
    /// Handles the creation of a customer subscription. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the created customer subscription.</param>
    public virtual Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a successful payment intent. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the successful payment intent.</param>
    public virtual Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a successful payment. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the successful payment.</param>
    public virtual Task PaymentSucceededAsync(PaymentSucceededContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a failed payment. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the failed payment.</param>
    public virtual Task PaymentFailedAsync(PaymentFailedContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a canceled payment. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the canceled payment.</param>
    public virtual Task PaymentCanceledAsync(PaymentCanceledContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a refund observed at the gateway. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the refund observed at the gateway.</param>
    public virtual Task PaymentRefundedAsync(PaymentRefundedContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Handles a dispute or chargeback opened against a settled payment. The default implementation does not perform any work.
    /// </summary>
    /// <param name="context">The context that describes the dispute.</param>
    public virtual Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context)
        => Task.CompletedTask;
}
