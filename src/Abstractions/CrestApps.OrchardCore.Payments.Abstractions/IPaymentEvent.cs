namespace CrestApps.OrchardCore.Payments;

public interface IPaymentEvent
{
    /// <summary>
    /// Triggered when a payment succeed.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task PaymentSucceededAsync(PaymentSucceededContext context);

    /// <summary>
    /// Triggered when a subscription is created.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context);

    /// <summary>
    /// Triggered when a payment intent succeed.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context);
}
