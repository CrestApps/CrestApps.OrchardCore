namespace CrestApps.OrchardCore.Payments;

public interface IPaymentEvent
{
    Task PaymentSucceededAsync(PaymentSucceededContext context);

    Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context);

    Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context);
}
