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
                Metadata = x.Metadata,
            }).ToList(),
            PaymentBehavior = "allow_incomplete",
            DefaultPaymentMethod = model.PaymentMethodId,
            Expand = ["latest_invoice.confirmation_secret"],
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

            // Stripe.net removed the phase 'Iterations' property. To limit the schedule to a fixed
            // number of billing cycles, we express the phase length as a duration derived from the
            // recurring interval of the price. All line items in a subscription group share the same
            // billing interval, so the first price is representative of the group.
            var phaseDuration = await GetPhaseDurationAsync(model.LineItems[0].PriceId, model.BillingCycles.Value);

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
                        Duration = phaseDuration,
                    }
                ]
            };

            var subscriptionScheduleService = new SubscriptionScheduleService(_stripeClient);
            await subscriptionScheduleService.CreateAsync(subscriptionScheduleOptions);
        }

        var confirmationSecret = subscription.LatestInvoice?.ConfirmationSecret;

        return new CreateSubscriptionResponse()
        {
            Id = subscription.Id,
            Status = subscription.Status,
            ClientSecret = confirmationSecret?.ClientSecret,
        };
    }

    private async Task<SubscriptionSchedulePhaseDurationOptions> GetPhaseDurationAsync(string priceId, int billingCycles)
    {
        var priceService = new PriceService(_stripeClient);
        var price = await priceService.GetAsync(priceId);

        // Default to monthly cadence when a price has no recurring configuration (e.g. one-time price).
        var interval = price?.Recurring?.Interval ?? "month";
        var intervalCount = price?.Recurring?.IntervalCount ?? 1;

        return new SubscriptionSchedulePhaseDurationOptions
        {
            Interval = interval,
            IntervalCount = intervalCount * billingCycles,
        };
    }
}
