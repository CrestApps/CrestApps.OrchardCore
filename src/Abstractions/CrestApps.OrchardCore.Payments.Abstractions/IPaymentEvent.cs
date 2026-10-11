namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Handles payment lifecycle events raised by the payment providers so features can react to successful
/// payments, failures, cancellations, refunds, disputes, and subscription creation. Every event is a
/// provider-neutral notification: the provider API stays authoritative when a webhook and local state
/// disagree, so handlers reconcile durable state rather than fabricate it from a notification.
/// </summary>
public interface IPaymentEvent
{
    /// <summary>
    /// Triggered when a payment succeeds.
    /// </summary>
    /// <param name="context">The context describing the successful payment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentSucceededAsync(PaymentSucceededContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a subscription is created.
    /// </summary>
    /// <param name="context">The context describing the created subscription.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a payment intent succeeds.
    /// </summary>
    /// <param name="context">The context describing the successful payment intent.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a payment fails at the gateway.
    /// </summary>
    /// <param name="context">The context describing the failed payment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentFailedAsync(PaymentFailedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a payment is canceled at the gateway.
    /// </summary>
    /// <param name="context">The context describing the canceled payment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentCanceledAsync(PaymentCanceledContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a refund is observed at the gateway, so the durable refund ledger can be reconciled.
    /// </summary>
    /// <param name="context">The context describing the refund observed at the gateway.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentRefundedAsync(PaymentRefundedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a dispute or chargeback is opened against a settled payment.
    /// </summary>
    /// <param name="context">The context describing the dispute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when a recurring-cycle payment fails at the gateway, so an existing subscription can enter a
    /// past-due state and start dunning instead of silently stopping.
    /// </summary>
    /// <param name="context">The context describing the failed cycle payment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscriptionPaymentFailedAsync(SubscriptionPaymentFailedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when the gateway reports a change to a hosted subscription's lifecycle state, including a
    /// cancellation made outside the application.
    /// </summary>
    /// <param name="context">The context describing the new state.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscriptionStatusChangedAsync(SubscriptionStatusChangedContext context, CancellationToken cancellationToken = default);
}
