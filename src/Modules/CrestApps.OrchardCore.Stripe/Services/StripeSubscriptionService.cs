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

        var usesInlinePrices = model.LineItems.Any(x => string.IsNullOrEmpty(x.PriceId));

        if (model.BillingCycles is > 0 && usesInlinePrices)
        {
            // A schedule needs catalog prices, which an inline price is not, so a cycle limit on an inline
            // agreement is expressed as the date the last cycle ends. Leaving it off would bill the customer
            // for as long as they forgot to cancel, on a plan that was sold as a fixed number of cycles.
            var inlinePrice = model.LineItems.First(x => x.Price is not null).Price;
            var billingStart = model.DeferralDays is > 0 ? now.AddDays(model.DeferralDays.Value) : now;

            subscriptionOptions.CancelAt = AdvanceCycles(billingStart, inlinePrice, model.BillingCycles.Value);
        }

        if (model.FirstCycleDiscount is > 0m)
        {
            // Stripe has no "charge less this once" on a subscription, but it has single-use coupons. One is
            // minted for this agreement so the recurring price stays what the plan says and only the first
            // invoice is reduced by what the checkout took off.
            var couponService = new CouponService(_stripeClient);
            var currency = model.LineItems.Select(x => x.Price?.Currency).FirstOrDefault(c => !string.IsNullOrEmpty(c));

            var coupon = await couponService.CreateAsync(new CouponCreateOptions
            {
                AmountOff = StripeCurrency.ToMinorUnits(model.FirstCycleDiscount.Value, currency),
                Currency = currency,
                Duration = "once",
                MaxRedemptions = 1,
                Name = "First cycle discount",
                Metadata = model.Metadata,
            }, model.ToRequestOptions("_coupon"));

            subscriptionOptions.Discounts = [new SubscriptionDiscountOptions { Coupon = coupon.Id }];
        }

        var subscriptionService = new SubscriptionService(_stripeClient);
        var subscription = await subscriptionService.CreateAsync(subscriptionOptions, model.ToRequestOptions());

        if (model.BillingCycles.HasValue && model.BillingCycles.Value > 0 && !usesInlinePrices)
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

    /// <inheritdoc/>
    public async Task<SubscriptionDetails> PauseAsync(PauseSubscriptionRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.SubscriptionId);

        var subscriptionService = new SubscriptionService(_stripeClient);

        // Stripe suspends collection with pause_collection and resumes by clearing it. 'void' is the right
        // behavior for a suspension the operator asked for: the cycles that pass while paused are not
        // invoiced at all, so resuming does not hand the customer a bill for time they did not have.
        var options = new SubscriptionUpdateOptions();

        if (model.Paused)
        {
            options.PauseCollection = new SubscriptionPauseCollectionOptions { Behavior = "void" };
        }
        else
        {
            options.PauseCollection = null;
            options.AddExtraParam("pause_collection", string.Empty);
        }

        var updated = await subscriptionService.UpdateAsync(model.SubscriptionId, options, model.ToRequestOptions());

        return ToDetails(updated);
    }

    // The moment the last of a fixed number of cycles ends, counted from when billing starts.
    private static DateTime AdvanceCycles(DateTime from, SubscriptionInlinePrice price, int cycles)
    {
        var count = Math.Max(1, price.IntervalCount) * Math.Max(1, cycles);

        return price.Interval switch
        {
            "day" => from.AddDays(count),
            "week" => from.AddDays(count * 7),
            "year" => from.AddYears(count),
            _ => from.AddMonths(count),
        };
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
                var price = lineItem.Price
                    ?? throw new InvalidOperationException("A subscription line item needs either a Stripe price id or an inline price.");

                // A named offer is created once and reused; an amount the buyer chose is sent inline,
                // because there is nothing to reuse and a price object per customer is only clutter.
                if (string.IsNullOrEmpty(price.LookupKey))
                {
                    options.PriceData = await BuildPriceDataAsync(price);
                }
                else
                {
                    options.Price = await EnsurePriceAsync(price);
                }
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

    // Finds the Stripe price for this offer, creating it the first time the offer is sold. The lookup key
    // is the offer's identity, so a concurrent first sale that loses the race still ends up on the same
    // price rather than on a duplicate.
    private async Task<string> EnsurePriceAsync(SubscriptionInlinePrice price)
    {
        var priceService = new PriceService(_stripeClient);

        var existing = await priceService.ListAsync(new PriceListOptions
        {
            LookupKeys = [price.LookupKey],
            Limit = 1,
        });

        if (existing?.Data?.Count > 0)
        {
            return existing.Data[0].Id;
        }

        try
        {
            var created = await priceService.CreateAsync(new PriceCreateOptions
            {
                Currency = price.Currency,
                UnitAmount = StripeCurrency.ToMinorUnits(price.UnitAmount, price.Currency),
                Product = string.IsNullOrEmpty(price.ProductId)
                    ? await EnsureProductAsync(price.ProductName)
                    : price.ProductId,
                Recurring = new PriceRecurringOptions
                {
                    Interval = price.Interval,
                    IntervalCount = price.IntervalCount <= 0 ? 1 : price.IntervalCount,
                },
                LookupKey = price.LookupKey,
            });

            return created.Id;
        }
        catch (StripeException)
        {
            // Another sale of the same offer created it between the lookup and the create. Reading it back
            // is right: both sales belong on the one price.
            var raced = await priceService.ListAsync(new PriceListOptions
            {
                LookupKeys = [price.LookupKey],
                Limit = 1,
            });

            if (raced?.Data?.Count > 0)
            {
                return raced.Data[0].Id;
            }

            throw;
        }
    }

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
