using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using OrchardCore.Modules;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Creates Stripe subscriptions and optional subscription schedules for Orchard Core subscription sessions.
/// </summary>
public sealed class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly StripeClient _stripeClient;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSubscriptionService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to create subscriptions and schedules.</param>
    /// <param name="clock">The clock used to calculate trial and schedule dates.</param>
    public StripeSubscriptionService(
        StripeClient stripeClient,
        IClock clock)
    {
        _stripeClient = stripeClient;
        _clock = clock;
    }

    /// <summary>
    /// Creates a Stripe subscription from the supplied subscription request.
    /// </summary>
    /// <param name="model">The subscription creation request.</param>
    /// <returns>The created subscription details, including any confirmation client secret.</returns>
    public async Task<CreateSubscriptionResponse> CreateAsync(CreateSubscriptionRequest model)
    {
        var now = _clock.UtcNow;

        var subscriptionOptions = new SubscriptionCreateOptions
        {
            Customer = model.CustomerId,
            Items = await BuildItemsAsync(model.LineItems),
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
        var subscription = await subscriptionService.CreateAsync(subscriptionOptions, model.ToRequestOptions());

        if (model.BillingCycles.HasValue && model.BillingCycles.Value > 0 && model.LineItems.All(x => !string.IsNullOrEmpty(x.PriceId)))
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

            // The schedule is created 'FromSubscription', so it is inherently bound to the (idempotent)
            // subscription created above; Stripe rejects a second schedule for the same subscription.
            // We intentionally do NOT attach an idempotency key here: the schedule's StartDate is derived
            // from the current time, so replaying a stored key with a later timestamp would trigger a
            // Stripe idempotency-parameter-mismatch error instead of returning the original schedule.
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

    /// <inheritdoc/>
    public async Task<SubscriptionDetails> GetAsync(string subscriptionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);

        var subscriptionService = new SubscriptionService(_stripeClient);

        var subscription = await subscriptionService.GetAsync(
            subscriptionId,
            new SubscriptionGetOptions { Expand = ["latest_invoice.confirmation_secret", "latest_invoice.payments"] });

        return ToDetails(subscription);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionDetails> CancelAsync(CancelSubscriptionRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.SubscriptionId);

        var subscriptionService = new SubscriptionService(_stripeClient);

        // Ending the agreement at the end of the paid period is a subscription update, not a cancellation:
        // Stripe's cancel API stops billing immediately, which would take away access the customer has
        // already paid for.
        if (model.AtPeriodEnd)
        {
            var updated = await subscriptionService.UpdateAsync(
                model.SubscriptionId,
                new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true,
                    CancellationDetails = new SubscriptionCancellationDetailsOptions { Comment = model.Reason },
                },
                model.ToRequestOptions());

            return ToDetails(updated);
        }

        var canceled = await subscriptionService.CancelAsync(
            model.SubscriptionId,
            new SubscriptionCancelOptions
            {
                CancellationDetails = new SubscriptionCancellationDetailsOptions { Comment = model.Reason },
            },
            model.ToRequestOptions());

        return ToDetails(canceled);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionDetails> UpdateAsync(UpdateSubscriptionRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.SubscriptionId);
        ArgumentNullException.ThrowIfNull(model.Price);

        var subscriptionService = new SubscriptionService(_stripeClient);
        var subscription = await subscriptionService.GetAsync(model.SubscriptionId);

        var existingItem = subscription.Items?.Data?.FirstOrDefault()
            ?? throw new InvalidOperationException($"The Stripe subscription '{model.SubscriptionId}' has no items to change.");

        var updated = await subscriptionService.UpdateAsync(
            model.SubscriptionId,
            new SubscriptionUpdateOptions
            {
                ProrationBehavior = model.ProrationBehavior,
                Items =
                [
                    new SubscriptionItemOptions
                    {
                        Id = existingItem.Id,
                        Quantity = model.Quantity,
                        PriceData = await BuildPriceDataAsync(model.Price),
                    }
                ],
            },
            model.ToRequestOptions());

        return ToDetails(updated);
    }

    private async Task<List<SubscriptionItemOptions>> BuildItemsAsync(IList<CreateSubscriptionLineItem> lineItems)
    {
        var items = new List<SubscriptionItemOptions>();

        foreach (var lineItem in lineItems ?? [])
        {
            var options = new SubscriptionItemOptions
            {
                Quantity = lineItem.Quantity,
                Metadata = lineItem.Metadata,
            };

            if (!string.IsNullOrEmpty(lineItem.PriceId))
            {
                options.Price = lineItem.PriceId;
            }
            else
            {
                options.PriceData = await BuildPriceDataAsync(lineItem.Price
                    ?? throw new InvalidOperationException("A subscription line item needs either a Stripe price id or an inline price."));
            }

            items.Add(options);
        }

        return items;
    }

    // Builds the inline recurring price. The amount crosses the Stripe boundary through StripeCurrency so a
    // zero-decimal currency such as JPY is not multiplied by a hundred.
    private async Task<SubscriptionItemPriceDataOptions> BuildPriceDataAsync(SubscriptionInlinePrice price)
        => new()
        {
            Currency = price.Currency,
            UnitAmount = StripeCurrency.ToMinorUnits(price.UnitAmount, price.Currency),
            Product = string.IsNullOrEmpty(price.ProductId)
                ? await EnsureProductAsync(price.ProductName)
                : price.ProductId,
            Recurring = new SubscriptionItemPriceDataRecurringOptions
            {
                Interval = price.Interval,
                IntervalCount = price.IntervalCount <= 0 ? 1 : price.IntervalCount,
            },
        };

    // Stripe requires an inline price to name a product, and it has no create-if-missing call. Deriving the
    // product id from the name makes the create idempotent: the same offer maps to one Stripe product no
    // matter how many customers subscribe to it, instead of littering the account with a product per checkout.
    private async Task<string> EnsureProductAsync(string productName)
    {
        var name = string.IsNullOrWhiteSpace(productName) ? "Subscription" : productName.Trim();
        var productId = BuildProductId(name);
        var productService = new ProductService(_stripeClient);

        try
        {
            var existing = await productService.GetAsync(productId);

            if (existing is not null)
            {
                return existing.Id;
            }
        }
        catch (StripeException exception) when (exception.StripeError?.Type == "invalid_request_error")
        {
            // The product does not exist yet, so it is created below.
        }

        try
        {
            var created = await productService.CreateAsync(new ProductCreateOptions
            {
                Id = productId,
                Name = name,
                Type = "service",
            });

            return created.Id;
        }
        catch (StripeException exception) when (exception.StripeError?.Code == "resource_already_exists")
        {
            // Another node created the same product between the read and the write. That is the outcome we
            // wanted, so it is not an error.
            return productId;
        }
    }

    // A deterministic, Stripe-safe id for the product behind an inline price.
    private static string BuildProductId(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));

        return "ca_" + Convert.ToHexStringLower(hash)[..24];
    }

    private static SubscriptionDetails ToDetails(Subscription subscription)
    {
        if (subscription is null)
        {
            return null;
        }

        var invoice = subscription.LatestInvoice;
        var currency = invoice?.Currency ?? subscription.Currency;

        return new SubscriptionDetails
        {
            Id = subscription.Id,
            Status = subscription.Status,
            Currency = currency,
            LiveMode = subscription.Livemode,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CurrentPeriodEndUtc = GetCurrentPeriodEnd(subscription),
            CanceledAtUtc = subscription.CanceledAt,
            LatestPaymentId = GetLatestPaymentId(invoice),
            AmountPaid = invoice is null || string.IsNullOrEmpty(currency)
                ? 0m
                : StripeCurrency.FromMinorUnits(invoice.AmountPaid, currency),
            ClientSecret = invoice?.ConfirmationSecret?.ClientSecret,
            LatestInvoicePaid = invoice is not null && string.Equals(invoice.Status, "paid", StringComparison.OrdinalIgnoreCase),
        };
    }

    // Stripe moved the period boundaries onto the individual items, so the subscription's own period is the
    // furthest item period rather than a property on the subscription itself.
    private static DateTime? GetCurrentPeriodEnd(Subscription subscription)
    {
        DateTime? end = null;

        foreach (var item in subscription.Items?.Data ?? [])
        {
            if (item.CurrentPeriodEnd != default && (end is null || item.CurrentPeriodEnd > end))
            {
                end = item.CurrentPeriodEnd;
            }
        }

        return end;
    }

    private static string GetLatestPaymentId(Invoice invoice)
    {
        foreach (var payment in invoice?.Payments?.Data ?? [])
        {
            var paymentIntentId = payment.Payment?.PaymentIntentId;

            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                return paymentIntentId;
            }
        }

        return invoice?.Id;
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
