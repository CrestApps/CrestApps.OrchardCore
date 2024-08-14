using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using OrchardCore.Modules;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly StripeClient _stripeClient;
    private readonly IClock _clock;

    public StripeSubscriptionService(
        StripeClient stripeClient,
        IClock clock)
    {
        _stripeClient = stripeClient;
        _clock = clock;
    }

    public async Task<CreateSubscriptionResponse> CreateAsync(CreateSubscriptionRequest model)
    {
        var now = _clock.UtcNow;

        var subscriptionOptions = new SubscriptionCreateOptions
        {
            Customer = model.CustomerId,
            Items = model.LineItems.Select(x => new SubscriptionItemOptions
            {
                Price = x.PriceId,
                Quantity = x.Quantity,
            }).ToList(),
            PaymentBehavior = "allow_incomplete",
            DefaultPaymentMethod = model.PaymentMethodId,
            // Expand = ["latest_invoice.payment_intent"],
            Metadata = model.Metadata,
        };

        if (model.TrialDuration.HasValue && model.TrialDuration.Value > 0)
        {
            subscriptionOptions.TrialEnd = model.TrialDurationType switch
            {
                DurationType.Day => now.AddDays(model.TrialDuration.Value),
                DurationType.Week => now.AddDays(model.TrialDuration.Value * 7),
                DurationType.Month => now.AddMonths(model.TrialDuration.Value),
                DurationType.Year => now.AddYears(model.TrialDuration.Value),
                _ => null
            };
        }

        var subscriptionService = new SubscriptionService(_stripeClient);
        var subscription = await subscriptionService.CreateAsync(subscriptionOptions);

        if (model.BillingCycles.HasValue && model.BillingCycles.Value > 0)
        {
            var phases = model.LineItems
                .Select(x => new SubscriptionSchedulePhaseItemOptions
                {
                    Price = x.PriceId,
                    Quantity = x.Quantity,
                }).ToList();

            var subscriptionScheduleOptions = new SubscriptionScheduleCreateOptions
            {
                FromSubscription = subscription.Id,
                Customer = model.CustomerId,
                StartDate = now,
                EndBehavior = "cancel",
                Phases =
                [
                    new SubscriptionSchedulePhaseOptions
                    {
                        Items = phases,
                        StartDate = now,
                        Iterations = model.BillingCycles.Value,
                    }
                ]
            };

            var subscriptionScheduleService = new SubscriptionScheduleService(_stripeClient);
            await subscriptionScheduleService.CreateAsync(subscriptionScheduleOptions);
        }

        return new CreateSubscriptionResponse()
        {
            Status = subscription.Status,
            ClientSecret = subscription.LatestInvoice?.PaymentIntent?.Status == "requires_action"
            ? subscription.LatestInvoice.PaymentIntent.ClientSecret
            : null,
        };
    }
}
