namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Handles payment lifecycle events raised by the payment providers so features can react to successful
/// payments and subscription creation.
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
}
