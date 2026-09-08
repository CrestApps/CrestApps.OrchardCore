using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// The Stripe implementation of <see cref="ICheckoutRecurringPaymentProvider"/>. It turns a recurring
/// checkout obligation into a real Stripe subscription, and lets that agreement be canceled or changed
/// later.
/// </summary>
/// <remarks>
/// The price is defined inline rather than looked up from a pre-created Stripe price. That is what makes
/// "subscribe to any product at any price point" possible: nothing has to be synchronized to Stripe before
/// it can be sold, so per-customer, computed, and one-off amounts all work.
///
/// Every write carries the attempt's idempotency key, so a retried begin resumes the same subscription
/// instead of billing the customer a second time.
/// </remarks>
public sealed class StripeRecurringPaymentProvider : ICheckoutRecurringPaymentProvider
{
    /// <summary>
    /// The key the client script uses to hand over the tokenized payment method the agreement is created
    /// against.
    /// </summary>
    public const string PaymentMethodDataKey = "paymentMethodId";

    /// <summary>
    /// The key the client script uses to reuse an existing Stripe customer rather than creating one.
    /// </summary>
    public const string CustomerDataKey = "customerId";

    private readonly IStripeSubscriptionService _subscriptionService;
    private readonly IStripeCustomerService _customerService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeRecurringPaymentProvider"/> class.
    /// </summary>
    /// <param name="subscriptionService">The Stripe subscription service.</param>
    /// <param name="customerService">The Stripe customer service.</param>
    /// <param name="logger">The logger.</param>
    public StripeRecurringPaymentProvider(
        IStripeSubscriptionService subscriptionService,
        IStripeCustomerService customerService,
        ILogger<StripeRecurringPaymentProvider> logger)
    {
        _subscriptionService = subscriptionService;
        _customerService = customerService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Key => StripeConstants.ProcessorKey;

    /// <inheritdoc/>
    public async Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);
        ArgumentNullException.ThrowIfNull(context.Interval);

        var attempt = context.Attempt;
        var paymentMethodId = GetProviderValue(context.ProviderData, PaymentMethodDataKey);

        if (string.IsNullOrEmpty(paymentMethodId))
        {
            // Stripe cannot bill a future cycle without a reusable payment method, and only the browser can
            // produce one. Failing here beats creating a subscription that can never collect.
            return PaymentBeginResult.Failure("A payment method is required before a recurring payment can be set up.");
        }

        var interval = MapInterval(context.Interval.Type);

        if (interval is null)
        {
            return PaymentBeginResult.Failure($"Stripe cannot bill on a '{context.Interval.Type}' interval.");
        }

        try
        {
            var customerId = GetProviderValue(context.ProviderData, CustomerDataKey);

            customerId ??= await CreateCustomerAsync(context, paymentMethodId);

            // The recurring price is the plan's cycle amount, never what is due now. What is due now is smaller
            // whenever a first-cycle coupon applied, and zero during a trial; a price taken from it would bill
            // the discount forever, or nothing at all.
            var grossAmount = context.CycleAmount;
            var firstCycleDiscount = context.TrialDays is > 0 ? 0m : Math.Max(0m, context.CycleAmount - context.FirstCycleAmount);

            var response = await _subscriptionService.CreateAsync(new CreateSubscriptionRequest
            {
                CustomerId = customerId,
                PaymentMethodId = paymentMethodId,
                IdempotencyKey = attempt.IdempotencyKey,
                Metadata = new Dictionary<string, string>
                {
                    ["checkout_attempt_id"] = attempt.ItemId,
                    ["checkout_session_id"] = attempt.SessionId,
                    ["checkout_obligation_id"] = attempt.ObligationId ?? string.Empty,
                },
                BillingCycles = GetBillingCycleLimit(context.LineItems),

                // Stripe holds the trial and starts billing when it ends, so the schedule survives a restart
                // of this application.
                TrialDuration = context.TrialDays,
                TrialDurationType = DurationType.Day,
                DeferralDays = context.TrialDays,
                FirstCycleDiscount = firstCycleDiscount > 0m ? firstCycleDiscount : null,
                LineItems =
                [
                    new CreateSubscriptionLineItem
                    {
                        Quantity = 1,
                        Price = new SubscriptionInlinePrice
                        {
                            UnitAmount = grossAmount,
                            Currency = attempt.Currency,
                            Interval = interval,
                            IntervalCount = context.Interval.Duration <= 0 ? 1 : context.Interval.Duration,
                            ProductName = BuildProductName(context.LineItems),
                        },
                    }
                ],
            });

            return new PaymentBeginResult
            {
                Succeeded = true,
                ProviderReference = response.Id,
                ClientSecret = response.ClientSecret,

                // A subscription that Stripe already activated needs nothing from the browser; only an
                // incomplete one does. Asking for confirmation that is not needed would stall the checkout.
                RequiresAction = response.RequiresAction,
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create a Stripe subscription for checkout attempt '{AttemptId}'.", attempt.ItemId);

            return PaymentBeginResult.Failure(exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.ProviderSubscriptionId);

        try
        {
            var details = await _subscriptionService.CancelAsync(new CancelSubscriptionRequest
            {
                SubscriptionId = context.ProviderSubscriptionId,
                AtPeriodEnd = context.AtPeriodEnd,
                Reason = context.Reason,
            });

            return RecurringCancelResult.Success(context.AtPeriodEnd
                ? details?.CurrentPeriodEndUtc
                : details?.CanceledAtUtc);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to cancel Stripe subscription '{SubscriptionId}'.", context.ProviderSubscriptionId);

            return RecurringCancelResult.Failure(exception.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.ProviderSubscriptionId);
        ArgumentNullException.ThrowIfNull(context.Interval);

        var interval = MapInterval(context.Interval.Type);

        if (interval is null)
        {
            return RecurringUpdateResult.Failure($"Stripe cannot bill on a '{context.Interval.Type}' interval.");
        }

        try
        {
            var details = await _subscriptionService.UpdateAsync(new UpdateSubscriptionRequest
            {
                SubscriptionId = context.ProviderSubscriptionId,
                Quantity = context.Quantity <= 0 ? 1 : context.Quantity,
                IdempotencyKey = context.IdempotencyKey,
                ProrationBehavior = MapProration(context.Proration),
                Price = new SubscriptionInlinePrice
                {
                    UnitAmount = context.Amount,
                    Currency = context.Currency,
                    Interval = interval,
                    IntervalCount = context.Interval.Duration <= 0 ? 1 : context.Interval.Duration,
                    ProductName = context.Description,
                },
            });

            return RecurringUpdateResult.Success(details?.CurrentPeriodEndUtc);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update Stripe subscription '{SubscriptionId}'.", context.ProviderSubscriptionId);

            return RecurringUpdateResult.Failure(exception.Message);
        }
    }

    private async Task<string> CreateCustomerAsync(BeginRecurringPaymentContext context, string paymentMethodId)
    {
        CheckoutContactInfo contact = null;

        context.Session?.TryGet(out contact);

        var customer = await _customerService.CreateAsync(new CreateCustomerRequest
        {
            Name = contact?.DisplayName,
            Email = contact?.Email,
            PaymentMethodId = paymentMethodId,

            // The customer is created inside the same retryable begin, so it carries its own derived key.
            // Without one, a retry would leave a duplicate customer behind for every attempt.
            IdempotencyKey = context.Attempt.IdempotencyKey + "_customer",
            Metadata = new Dictionary<string, string>
            {
                ["checkout_session_id"] = context.Attempt.SessionId,
            },
        });

        return customer.CustomerId;
    }

    // Stripe expects the interval as its own vocabulary, and it has no concept of an interval outside these
    // four. An unmappable one is reported rather than silently coerced into a different billing period.
    private static string MapInterval(DurationType durationType)
        => durationType switch
        {
            DurationType.Day => "day",
            DurationType.Week => "week",
            DurationType.Month => "month",
            DurationType.Year => "year",
            _ => null,
        };

    private static string MapProration(RecurringProrationBehavior proration)
        => proration switch
        {
            RecurringProrationBehavior.None => "none",
            RecurringProrationBehavior.AlwaysInvoice => "always_invoice",
            _ => "create_prorations",
        };

    // The lines in one interval group can each cap the number of cycles. The lowest cap wins, because
    // billing past any line's limit would charge for something the customer did not agree to.
    private static int? GetBillingCycleLimit(IEnumerable<CheckoutLineItem> lineItems)
    {
        int? limit = null;

        foreach (var lineItem in lineItems ?? [])
        {
            var cycles = lineItem.Plan?.BillingCycleLimit;

            if (cycles > 0 && (limit is null || cycles < limit))
            {
                limit = cycles;
            }
        }

        return limit;
    }

    // The product name is what the customer reads on their Stripe invoice, so it describes what they bought
    // rather than an internal identifier.
    private static string BuildProductName(IEnumerable<CheckoutLineItem> lineItems)
    {
        var descriptions = (lineItems ?? [])
            .Select(lineItem => lineItem.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return descriptions.Length == 0 ? "Subscription" : string.Join(", ", descriptions);
    }

    private static string GetProviderValue(IReadOnlyDictionary<string, string> providerData, string key)
    {
        if (providerData is not null && providerData.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return null;
    }
}
