namespace CrestApps.OrchardCore.Payments;

public abstract class PaymentEventBase : IPaymentEvent
{
    public virtual Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
        => Task.FromResult(Task.CompletedTask);

    public virtual Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
        => Task.FromResult(Task.CompletedTask);

    public virtual Task PaymentSucceededAsync(PaymentSucceededContext context)
        => Task.FromResult(Task.CompletedTask);
}
