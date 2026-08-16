using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

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
    private readonly IStripeRefundService _refundService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutPaymentProvider"/> class.
    /// </summary>
    /// <param name="paymentIntentService">The Stripe PaymentIntent service.</param>
    /// <param name="refundService">The Stripe refund service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer used for the display name.</param>
    public StripeCheckoutPaymentProvider(
        IStripePaymentIntentService paymentIntentService,
        IStripeRefundService refundService,
        ILogger<StripeCheckoutPaymentProvider> logger,
        IStringLocalizer<StripeCheckoutPaymentProvider> stringLocalizer)
    {
        _paymentIntentService = paymentIntentService;
        _refundService = refundService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key => StripeConstants.ProcessorKey;

    /// <inheritdoc/>
    public string DisplayName => S["Credit or debit card (Stripe)"];

    /// <inheritdoc/>
    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = false,
        SupportsHostedCheckout = false,
        SupportsEmbeddedElements = true,
        SupportsCombinedOneTimeAndRecurring = false,
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
        var grossAmount = (decimal)attempt.ExpectedAmount + (decimal)attempt.ExpectedTaxAmount;

        try
        {
            var response = await _paymentIntentService.CreateForCheckoutAsync(new CreateCheckoutPaymentIntentRequest
            {
                Amount = grossAmount,
                Currency = attempt.Currency,

                // A stable idempotency key makes retrying BeginAsync return the same PaymentIntent instead
                // of creating a duplicate charge.
                IdempotencyKey = attempt.IdempotencyKey,
                Metadata = new Dictionary<string, string>
                {
                    ["checkout_attempt_id"] = attempt.Id,
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
            _logger.LogError(ex, "Failed to create a Stripe PaymentIntent for checkout attempt '{AttemptId}'.", attempt.Id);

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
            _logger.LogError(ex, "Failed to retrieve Stripe PaymentIntent '{PaymentIntentId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.Id);

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
                var taxAmount = (decimal)attempt.ExpectedTaxAmount;
                var netCharged = grossCharged - taxAmount;

                return new PaymentVerificationResult
                {
                    Status = PaymentStatus.Succeeded,
                    ReportsAuthoritativeAmount = true,
                    TransactionId = intent.Id,
                    Amount = (double)netCharged,
                    TaxAmount = (double)taxAmount,
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
                var grossCharged = StripeCurrency.FromMinorUnits(intent.AmountReceived, intent.Currency);

                var refund = await _refundService.CreateAsync(new CreateRefundRequest
                {
                    PaymentIntentId = intent.Id,
                    Amount = grossCharged,
                    Currency = intent.Currency,
                    Reason = "requested_by_customer",
                    IdempotencyKey = "cancel_" + attempt.Id,
                });

                return refund.Status == "failed"
                    ? PaymentCancelResult.Failure(refund.FailureReason ?? "The Stripe refund failed.")
                    : refund.Status == "succeeded"
                        ? PaymentCancelResult.Success()
                        : PaymentCancelResult.Pending();
            }

            await _paymentIntentService.CancelAsync(new CancelPaymentIntentRequest
            {
                PaymentIntentId = intent.Id,
                CancellationReason = "abandoned",
                IdempotencyKey = "cancel_" + attempt.Id,
            });

            return PaymentCancelResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel Stripe PaymentIntent '{PaymentIntentId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.Id);

            return PaymentCancelResult.Failure(ex.Message);
        }
    }

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
