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
    Task PaymentSucceededAsync(PaymentSucceededContext context);

    /// <summary>
    /// Triggered when a subscription is created.
    /// </summary>
    /// <param name="context">The context describing the created subscription.</param>
    Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context);

    /// <summary>
    /// Triggered when a payment intent succeeds.
    /// </summary>
    /// <param name="context">The context describing the successful payment intent.</param>
    Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context);

    /// <summary>
    /// Triggered when a payment fails at the gateway.
    /// </summary>
    /// <param name="context">The context describing the failed payment.</param>
    Task PaymentFailedAsync(PaymentFailedContext context);

    /// <summary>
    /// Triggered when a payment is canceled at the gateway.
    /// </summary>
    /// <param name="context">The context describing the canceled payment.</param>
    Task PaymentCanceledAsync(PaymentCanceledContext context);

    /// <summary>
    /// Triggered when a refund is observed at the gateway, so the durable refund ledger can be reconciled.
    /// </summary>
    /// <param name="context">The context describing the refund observed at the gateway.</param>
    Task PaymentRefundedAsync(PaymentRefundedContext context);

    /// <summary>
    /// Triggered when a dispute or chargeback is opened against a settled payment.
    /// </summary>
    /// <param name="context">The context describing the dispute.</param>
    Task PaymentDisputeCreatedAsync(PaymentDisputeCreatedContext context);
}
