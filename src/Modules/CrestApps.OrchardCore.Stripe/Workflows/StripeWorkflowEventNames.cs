namespace CrestApps.OrchardCore.Stripe.Workflows;

/// <summary>
/// Defines the names of the workflow events raised by the Stripe integration.
/// </summary>
public static class StripeWorkflowEventNames
{
    /// <summary>
    /// The event raised when an invoice payment succeeds (including subscription cycles).
    /// </summary>
    public const string PaymentReceived = "StripePaymentReceivedEvent";

    /// <summary>
    /// The event raised when a Stripe subscription is created.
    /// </summary>
    public const string SubscriptionCreated = "StripeSubscriptionCreatedEvent";

    /// <summary>
    /// The event raised when a payment intent succeeds.
    /// </summary>
    public const string PaymentIntentSucceeded = "StripePaymentIntentSucceededEvent";

    /// <summary>
    /// The event raised when a payment fails at the gateway.
    /// </summary>
    public const string PaymentFailed = "StripePaymentFailedEvent";

    /// <summary>
    /// The event raised when a payment is canceled at the gateway.
    /// </summary>
    public const string PaymentCanceled = "StripePaymentCanceledEvent";

    /// <summary>
    /// The event raised when a refund is observed at the gateway.
    /// </summary>
    public const string PaymentRefunded = "StripePaymentRefundedEvent";

    /// <summary>
    /// The event raised when a dispute or chargeback is opened.
    /// </summary>
    public const string DisputeCreated = "StripeDisputeCreatedEvent";

    /// <summary>
    /// The event raised when a request to Stripe fails, typically due to an authentication or connectivity problem.
    /// </summary>
    public const string RequestFailed = "StripeRequestFailedEvent";
}
