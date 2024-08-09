using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly SubscriptionService _subscriptionService;

    public StripeSubscriptionService(StripeClient stripeClient)
    {
        _subscriptionService = new SubscriptionService(stripeClient);
    }

    public async Task<CreateSubscriptionResponse> CreateAsync(CreateSubscriptionRequest model)
    {
        var subscriptionOptions = new SubscriptionCreateOptions
        {
            Customer = model.CustomerId,
            Items =
            [
                new()
                {
                    Plan = model.PlanId,
                },
            ],
            DefaultPaymentMethod = model.PaymentMethodId,
            Expand = ["latest_invoice.payment_intent"],
            Metadata = model.Metadata,
        };

        var subscription = await _subscriptionService.CreateAsync(subscriptionOptions);

        return new CreateSubscriptionResponse()
        {
            Status = subscription.Status,
            ClientSecret = subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action"
            ? subscription.LatestInvoice.PaymentIntent.ClientSecret
            : null,
        };
    }
}
