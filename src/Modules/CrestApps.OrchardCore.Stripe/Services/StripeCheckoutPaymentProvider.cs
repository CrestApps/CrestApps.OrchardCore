using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// A generic <see cref="ICheckoutPaymentProvider"/> that settles a one-time checkout payment through a
/// Stripe PaymentIntent, and an <see cref="ICheckoutPaymentRefundProvider"/> that refunds it. It is the
/// piece that lets any checkout — subscriptions today, a future storefront tomorrow — collect a card
/// payment through Stripe without depending on the subscription-specific endpoints. All money crosses the
/// Stripe boundary through <see cref="StripeCurrency"/>, and verification always queries Stripe's
/// authoritative API rather than trusting a cached webhook so an obligation is never marked paid when the
/// gateway actually failed.
/// </summary>
public sealed class StripeCheckoutPaymentProvider : ICheckoutPaymentProvider, ICheckoutPaymentRefundProvider
{
    private readonly IStripePaymentIntentService _paymentIntentService;
    private readonly IStripeSubscriptionService _subscriptionService;
    private readonly IStripeRefundService _refundService;
    private readonly IStripeCheckoutCustomerResolver _customerResolver;
    private readonly IPaymentRefundStore _refundStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutPaymentProvider"/> class.
    /// </summary>
    /// <param name="paymentIntentService">The Stripe PaymentIntent service.</param>
    /// <param name="subscriptionService">The Stripe subscription service, used to verify recurring obligations.</param>
    /// <param name="refundService">The Stripe refund service.</param>
    /// <param name="customerResolver">The resolver for the customer this checkout belongs to.</param>
    /// <param name="refundStore">The durable refund ledger used to record compensation refunds.</param>
    /// <param name="clock">The clock used to stamp refund completion.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer used for the display name.</param>
    public StripeCheckoutPaymentProvider(
        IStripePaymentIntentService paymentIntentService,
        IStripeSubscriptionService subscriptionService,
        IStripeRefundService refundService,
        IStripeCheckoutCustomerResolver customerResolver,
        IPaymentRefundStore refundStore,
        IClock clock,
        ILogger<StripeCheckoutPaymentProvider> logger,
        IStringLocalizer<StripeCheckoutPaymentProvider> stringLocalizer)
    {
        _paymentIntentService = paymentIntentService;
        _subscriptionService = subscriptionService;
        _refundService = refundService;
        _customerResolver = customerResolver;
        _refundStore = refundStore;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    private static string GetProviderValue(IReadOnlyDictionary<string, string> providerData, string key)
    {
        if (providerData is not null && providerData.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return null;
    }

    /// <inheritdoc/>
    public string Key => StripeConstants.ProcessorKey;

    /// <inheritdoc/>
    public string DisplayName => S["Credit or debit card (Stripe)"];

    /// <inheritdoc/>
    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,

        // Recurring is honored by StripeRecurringPaymentProvider, which creates a real Stripe subscription
        // for the obligation. The two capabilities share this provider key, so verification and cancellation
        // below handle both a PaymentIntent and a subscription reference.
        SupportsRecurringPayments = true,
        SupportsHostedCheckout = false,
        SupportsEmbeddedElements = true,
        SupportsCombinedOneTimeAndRecurring = true,
        CollectsTaxDynamically = false,
        SupportsRefunds = true,
    };

    /// <inheritdoc/>
    public async Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        var attempt = context.Attempt;

        // Stripe charges a single gross amount; the checkout composed it from the taxable base plus the
        // tax it determined, so the intent is created for base + tax.
        var grossAmount = attempt.ExpectedAmount + attempt.ExpectedTaxAmount;

        try
        {
            // When the same checkout also establishes a recurring agreement, the browser tokenizes one
            // reusable payment method and confirms every obligation with it. Stripe attaches that payment
            // method to a customer, and then refuses to confirm an intent that does not name the same one,
            // so a setup fee billed alongside a plan has to be created against that customer. A checkout
            // with no recurring obligation sends no payment method here and stays customer-less.
            var customerId = await _customerResolver.ResolveAsync(
                context.Session,
                attempt.SessionId,
                GetProviderValue(context.ProviderData, StripeRecurringPaymentProvider.PaymentMethodDataKey),
                GetProviderValue(context.ProviderData, StripeRecurringPaymentProvider.CustomerDataKey));

            var response = await _paymentIntentService.CreateForCheckoutAsync(new CreateCheckoutPaymentIntentRequest
            {
                Amount = grossAmount,
                Currency = attempt.Currency,
                CustomerId = customerId,

                // A stable idempotency key makes retrying BeginAsync return the same PaymentIntent instead
                // of creating a duplicate charge.
                IdempotencyKey = attempt.IdempotencyKey,
                Metadata = new Dictionary<string, string>
                {
                    ["checkout_attempt_id"] = attempt.ItemId,
                    ["checkout_session_id"] = attempt.SessionId,
                },
            });

            return new PaymentBeginResult
            {
                Succeeded = true,
                ProviderReference = response.Id,
                ClientSecret = response.ClientSecret,
                RequiresAction = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create a Stripe PaymentIntent for checkout attempt '{AttemptId}'.", attempt.ItemId);

            return PaymentBeginResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        var attempt = context.Attempt;

        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            // The attempt was created but the PaymentIntent was never begun, so there is nothing to
            // confirm yet. Leave the obligation outstanding rather than inventing a settlement.
            return new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            };
        }

        // A recurring obligation is settled by a Stripe subscription, not a PaymentIntent. Reading the
        // subscription back is the only way to know whether its first invoice was actually paid.
        if (IsSubscriptionReference(attempt.ProviderReference))
        {
            return await VerifySubscriptionAsync(attempt);
        }

        PaymentIntentDetails intent;

        try
        {
            intent = await _paymentIntentService.RetrieveAsync(new RetrievePaymentIntentRequest
            {
                PaymentIntentId = attempt.ProviderReference,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Stripe PaymentIntent '{PaymentIntentId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.ItemId);

            // A transient retrieval failure is not an authoritative outcome; leave the attempt pending so a
            // later reconciliation can settle it.
            return new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            };
        }

        var gatewayMode = intent.LiveMode ? GatewayMode.Live : GatewayMode.Testing;

        switch (intent.Status)
        {
            case "succeeded":
                // Stripe reports the gross it actually collected. The tax is the immutable amount the
                // checkout captured on the attempt, so the net base the framework validates is the gross
                // Stripe collected minus that tax. This both settles the correct net/tax split and lets the
                // framework reject a settlement whose gross fell short of what was expected.
                var grossCharged = StripeCurrency.FromMinorUnits(intent.AmountReceived, intent.Currency);
                var taxAmount = attempt.ExpectedTaxAmount;
                var netCharged = grossCharged - taxAmount;

                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Succeeded,
                    ReportsAuthoritativeAmount = true,
                    TransactionId = intent.Id,
                    Amount = netCharged,
                    TaxAmount = taxAmount,
                    TaxSnapshot = attempt.TaxSnapshot,
                    Currency = intent.Currency,
                    GatewayMode = gatewayMode,
                };

            case "canceled":
                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Failed,
                    TransactionId = intent.Id,
                    Currency = intent.Currency,
                    GatewayMode = gatewayMode,
                };

            default:
                // requires_payment_method, requires_confirmation, requires_action, processing, ...: the
                // customer has not finished paying. Leave the obligation outstanding.
                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Unknown,
                    Currency = intent.Currency,
                    GatewayMode = gatewayMode,
                };
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        var attempt = context.Attempt;

        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            // Nothing was created at the gateway, so there is nothing to void.
            return PaymentCancelResult.Success();
        }

        if (IsSubscriptionReference(attempt.ProviderReference))
        {
            return await CancelSubscriptionAsync(attempt);
        }

        try
        {
            var intent = await _paymentIntentService.RetrieveAsync(new RetrievePaymentIntentRequest
            {
                PaymentIntentId = attempt.ProviderReference,
            });

            if (intent.Status == "canceled")
            {
                return PaymentCancelResult.Success();
            }

            if (intent.Status == "succeeded")
            {
                // The charge already went through, so compensation is a refund of the gross collected
                // rather than a void.
                return await CompensateSucceededChargeAsync(intent, attempt, cancellationToken);
            }

            await _paymentIntentService.CancelAsync(new CancelPaymentIntentRequest
            {
                PaymentIntentId = intent.Id,
                CancellationReason = "abandoned",
                IdempotencyKey = "cancel_" + attempt.ItemId,
            });

            return PaymentCancelResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel Stripe PaymentIntent '{PaymentIntentId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.ItemId);

            return PaymentCancelResult.Failure(ex.Message);
        }
    }

    // Stripe object ids are prefixed by type, so the prefix is what tells a subscription agreement apart from
    // a single charge without having to store the distinction separately on the attempt.
    private static bool IsSubscriptionReference(string providerReference)
        => providerReference is not null && providerReference.StartsWith("sub_", StringComparison.Ordinal);

    // Verifies a recurring obligation. The agreement only counts as settled once Stripe reports the first
    // invoice paid: an 'active' subscription whose invoice is still open has collected nothing.
    private async Task<PaymentVerificationResult> VerifySubscriptionAsync(PaymentAttempt attempt)
    {
        SubscriptionDetails subscription;

        try
        {
            subscription = await _subscriptionService.GetAsync(attempt.ProviderReference);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve Stripe subscription '{SubscriptionId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.ItemId);

            return new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            };
        }

        if (subscription is null)
        {
            return new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            };
        }

        var gatewayMode = subscription.LiveMode ? GatewayMode.Live : GatewayMode.Testing;
        var currency = subscription.Currency ?? attempt.Currency;

        // A trial has no first payment to confirm, yet the agreement is genuinely established, so it settles
        // the obligation for the amount actually collected, which is zero.
        var isTrial = string.Equals(subscription.Status, "trialing", StringComparison.OrdinalIgnoreCase);

        if (subscription.LatestInvoicePaid || isTrial)
        {
            var collectedNothing = isTrial && !subscription.LatestInvoicePaid;

            // A trial collects nothing, so neither the net nor the tax may be reported as charged.
            // Subtracting the expected tax from a zero gross would settle the obligation for a negative
            // amount, which is money the ledger would then believe was taken from the customer.
            var grossCharged = collectedNothing ? 0m : subscription.AmountPaid;
            var taxAmount = collectedNothing ? 0m : attempt.ExpectedTaxAmount;

            return new PaymentVerificationResult
            {
                Status = PaymentStatus.Succeeded,
                ReportsAuthoritativeAmount = true,
                TransactionId = subscription.LatestPaymentId ?? subscription.Id,
                Amount = grossCharged - taxAmount,
                TaxAmount = taxAmount,
                TaxSnapshot = attempt.TaxSnapshot,
                Currency = currency,
                GatewayMode = gatewayMode,
            };
        }

        switch (subscription.Status)
        {
            case "canceled":
            case "incomplete_expired":
                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Failed,
                    TransactionId = subscription.Id,
                    Currency = currency,
                    GatewayMode = gatewayMode,
                };

            default:
                // incomplete, past_due, unpaid, or paid-status not yet reported: the first cycle has not
                // collected, so the obligation stays outstanding for a later reconciliation.
                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Unknown,
                    Currency = currency,
                    GatewayMode = gatewayMode,
                };
        }
    }

    // Rolls back a recurring agreement. Unlike a single charge there is nothing to void, so the agreement is
    // ended immediately: the checkout it belonged to is not completing, so the customer never gets what the
    // future cycles would have paid for.
    private async Task<PaymentCancelResult> CancelSubscriptionAsync(PaymentAttempt attempt)
    {
        try
        {
            await _subscriptionService.CancelAsync(new CancelSubscriptionRequest
            {
                SubscriptionId = attempt.ProviderReference,
                AtPeriodEnd = false,
                Reason = "The checkout this subscription belonged to was rolled back.",
                IdempotencyKey = "cancel_" + attempt.ItemId,
            });

            return PaymentCancelResult.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to cancel Stripe subscription '{SubscriptionId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.ItemId);

            return PaymentCancelResult.Failure(exception.Message);
        }
    }

    // Refunds a charge that already succeeded for an attempt the checkout is rolling back, and records that
    // refund in the durable refund ledger. Writing the ledger entry is not bookkeeping niceness: without it the
    // gateway's own 'charge.refunded' notification finds no local request, and the reconciliation service has to
    // quarantine a refund this application itself issued for an operator to resolve by hand. The record is
    // written before the gateway is called, exactly as the refund service does, so a crash cannot strand a real
    // refund.
    private async Task<PaymentCancelResult> CompensateSucceededChargeAsync(
        PaymentIntentDetails intent,
        PaymentAttempt attempt,
        CancellationToken cancellationToken)
    {
        var grossCharged = StripeCurrency.FromMinorUnits(intent.AmountReceived, intent.Currency);
        var idempotencyKey = "cancel_" + attempt.ItemId;

        // A retried cancellation must reuse the record the first attempt created rather than adding a second
        // one, so the refunded total is never double-counted against the charge.
        var record = await _refundStore.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (record is null)
        {
            record = BuildCompensationRefund(intent, attempt, grossCharged, idempotencyKey);

            await _refundStore.CreateAsync(record, cancellationToken);
        }
        else if (record.Status is RefundStatus.Succeeded or RefundStatus.Failed or RefundStatus.Canceled)
        {
            // The gateway already reported a terminal outcome for this compensation; do not call it again.
            return record.Status == RefundStatus.Succeeded
                ? PaymentCancelResult.Success()
                : PaymentCancelResult.Failure(record.FailureReason ?? "The Stripe refund failed.");
        }

        RefundDetails refund;

        try
        {
            refund = await _refundService.CreateAsync(new CreateRefundRequest
            {
                PaymentIntentId = intent.Id,
                Amount = grossCharged,
                Currency = intent.Currency,
                Reason = "requested_by_customer",
                IdempotencyKey = idempotencyKey,
            });
        }
        catch (Exception exception)
        {
            // The gateway mutation is unconfirmed, so the refund stays non-terminal for a later reconciliation
            // rather than being reported as either done or failed.
            record.Status = RefundStatus.Pending;
            record.FailureReason = exception.Message;

            await _refundStore.UpdateAsync(record, cancellationToken);

            _logger.LogError(exception, "Failed to compensate Stripe PaymentIntent '{PaymentIntentId}' for checkout attempt '{AttemptId}'.", intent.Id, attempt.ItemId);

            return PaymentCancelResult.Failure(exception.Message);
        }

        record.ProviderRefundReference = refund.Id;
        record.FailureReason = refund.FailureReason;

        record.Status = refund.Status switch
        {
            "succeeded" => RefundStatus.Succeeded,
            "failed" or "canceled" => RefundStatus.Failed,
            _ => RefundStatus.Pending,
        };

        if (record.Status is RefundStatus.Succeeded or RefundStatus.Failed)
        {
            record.CompletedUtc = _clock.UtcNow;
        }

        await _refundStore.UpdateAsync(record, cancellationToken);

        return record.Status switch
        {
            RefundStatus.Succeeded => PaymentCancelResult.Success(),
            RefundStatus.Failed => PaymentCancelResult.Failure(refund.FailureReason ?? "The Stripe refund failed."),
            _ => PaymentCancelResult.Pending(),
        };
    }

    // Builds the ledger entry for a compensation refund. Compensation always returns the whole charge, so the
    // attempt's immutable tax snapshot applies in full and its captured amounts are used verbatim — the same
    // allocation a full refund through the refund service produces, without recalculating tax with today's rules.
    private PaymentRefund BuildCompensationRefund(
        PaymentIntentDetails intent,
        PaymentAttempt attempt,
        decimal grossCharged,
        string idempotencyKey)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = attempt.SessionId,
            ProviderKey = Key,
            OriginalAttemptId = attempt.ItemId,
            OriginalTransactionId = string.IsNullOrEmpty(attempt.TransactionId) ? intent.Id : attempt.TransactionId,
            ObligationId = attempt.ObligationId,
            Currency = attempt.Currency ?? intent.Currency,
            RefundGrossAmount = grossCharged,
            RefundTaxAmount = attempt.ConfirmedTaxAmount,
            RefundTaxableAmount = attempt.ConfirmedAmount,
            TaxLines = attempt.TaxSnapshot?.Lines is { Count: > 0 } lines ? [.. lines] : [],
            Status = RefundStatus.Requested,
            Reason = "Checkout compensation for a rolled-back payment attempt.",
            IdempotencyKey = idempotencyKey,
            GatewayMode = attempt.GatewayMode,
        };

    /// <inheritdoc/>
    public async Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.OriginalTransactionId);

        try
        {
            var refund = await _refundService.CreateAsync(new CreateRefundRequest
            {
                PaymentIntentId = context.OriginalTransactionId,
                Amount = context.Amount,
                Currency = context.Currency,
                Reason = "requested_by_customer",

                // The refund record's idempotency key makes a retried refund return the same Stripe refund
                // instead of refunding twice.
                IdempotencyKey = context.IdempotencyKey,
            });

            return refund.Status switch
            {
                "succeeded" => PaymentRefundResult.Success(refund.Id, StripeCurrency.FromMinorUnits(refund.Amount, refund.Currency), refund.Currency, context.GatewayMode),
                "failed" or "canceled" => PaymentRefundResult.Failed(refund.Status, refund.FailureReason),
                _ => PaymentRefundResult.Pending(refund.Id, context.GatewayMode),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refund Stripe payment '{TransactionId}'.", context.OriginalTransactionId);

            return PaymentRefundResult.Failed("stripe_error", ex.Message);
        }
    }
}
