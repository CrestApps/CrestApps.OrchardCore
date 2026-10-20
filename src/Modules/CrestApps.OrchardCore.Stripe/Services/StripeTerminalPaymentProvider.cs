using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using Stripe;
using Stripe.Terminal;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// A card presented on a Stripe Terminal reader at a register. The server tells the reader to collect the payment
/// (Stripe's server-driven integration), so the till app needs no Stripe SDK: it begins the payment with the reader's
/// id and asks whether it completed. It's offered only for a register's sale.
/// </summary>
public sealed class StripeTerminalPaymentProvider : ICheckoutPaymentProvider, ICheckoutPaymentProviderAvailability, ICheckoutPaymentProviderDescription, ICheckoutPaymentRefundProvider
{
    /// <summary>
    /// The provider data a till sends the reader's id in.
    /// </summary>
    public const string ReaderIdDataKey = "readerId";

    private readonly StripeClient _stripeClient;
    private readonly StripeOptions _options;
    private readonly IStripeRefundService _refundService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public StripeTerminalPaymentProvider(
        StripeClient stripeClient,
        IOptions<StripeOptions> options,
        IStripeRefundService refundService,
        ILogger<StripeTerminalPaymentProvider> logger,
        IStringLocalizer<StripeTerminalPaymentProvider> stringLocalizer)
    {
        _stripeClient = stripeClient;
        _options = options.Value;
        _refundService = refundService;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Key
        => StripeConstants.TerminalProviderKey;

    public string DisplayName
        => S["Card on a Stripe reader"];

    public string Description
        => S["The buyer taps, inserts or swipes their card on the register's Stripe Terminal reader."];

    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRefunds = true,
    };

    public Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
        => Task.FromResult(_options.IsConfigured && session is not null && session.TryGet<CheckoutCounterSale>(out _));

    public async Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Session is not { } session || !session.TryGet<CheckoutCounterSale>(out _))
        {
            return PaymentBeginResult.Failure(S["A reader payment is only for a sale rung up at a register."]);
        }

        if (context.ProviderData is null || !context.ProviderData.TryGetValue(ReaderIdDataKey, out var readerId) || string.IsNullOrWhiteSpace(readerId))
        {
            return PaymentBeginResult.Failure(S["Say which Stripe reader takes the card."]);
        }

        var attempt = context.Attempt;

        try
        {
            // A begin sent again for the same attempt reuses its payment rather than charging the card twice.
            var paymentIntents = new PaymentIntentService(_stripeClient);
            var paymentIntent = string.IsNullOrEmpty(attempt.ProviderReference)
                ? await paymentIntents.CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = StripeCurrency.ToMinorUnits(attempt.ExpectedAmount + attempt.ExpectedTaxAmount, attempt.Currency),
                    Currency = attempt.Currency.ToLowerInvariant(),
                    PaymentMethodTypes = ["card_present"],
                    CaptureMethod = "automatic",
                    Metadata = new Dictionary<string, string>
                    {
                        ["checkoutSessionId"] = session.SessionId,
                        ["paymentAttemptId"] = attempt.ItemId,
                    },
                }, new RequestOptions { IdempotencyKey = "terminal-pi-" + attempt.ItemId }, cancellationToken)
                : await paymentIntents.GetAsync(attempt.ProviderReference, cancellationToken: cancellationToken);

            await new ReaderService(_stripeClient).ProcessPaymentIntentAsync(readerId.Trim(), new ReaderProcessPaymentIntentOptions
            {
                PaymentIntent = paymentIntent.Id,
            }, cancellationToken: cancellationToken);

            // The reader collects the card; the checkout asks Stripe whether it was paid.
            return PaymentBeginResult.Success(paymentIntent.Id);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "The Stripe reader '{ReaderId}' couldn't take the payment of checkout '{SessionId}'.", readerId, session.SessionId);

            return PaymentBeginResult.Failure(S["The reader couldn't take the payment: {0}", exception.StripeError?.Message ?? exception.Message]);
        }
    }

    public async Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attempt = context.Attempt;

        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            return new PaymentVerificationResult { Status = PaymentStatus.Unknown };
        }

        try
        {
            var paymentIntent = await new PaymentIntentService(_stripeClient).GetAsync(attempt.ProviderReference, cancellationToken: cancellationToken);

            return paymentIntent.Status switch
            {
                "succeeded" => new PaymentVerificationResult
                {
                    Status = PaymentStatus.Succeeded,
                    ReportsAuthoritativeAmount = true,
                    TransactionId = paymentIntent.Id,
                    Amount = StripeCurrency.FromMinorUnits(paymentIntent.AmountReceived, paymentIntent.Currency) - attempt.ExpectedTaxAmount,
                    TaxAmount = attempt.ExpectedTaxAmount,
                    TaxSnapshot = attempt.TaxSnapshot,
                    Currency = paymentIntent.Currency.ToUpperInvariant(),
                    GatewayMode = paymentIntent.Livemode ? GatewayMode.Live : GatewayMode.Testing,
                },
                "canceled" => new PaymentVerificationResult { Status = PaymentStatus.Failed, Currency = attempt.Currency },

                // Waiting for the card on the reader, or for the bank.
                _ => new PaymentVerificationResult { Status = PaymentStatus.Unknown, Currency = attempt.Currency },
            };
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "The Stripe reader payment '{PaymentIntentId}' couldn't be checked.", attempt.ProviderReference);

            return new PaymentVerificationResult { Status = PaymentStatus.Unknown, Currency = attempt.Currency };
        }
    }

    public async Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.Attempt?.ProviderReference))
        {
            return PaymentCancelResult.Success();
        }

        try
        {
            await new PaymentIntentService(_stripeClient).CancelAsync(context.Attempt.ProviderReference, cancellationToken: cancellationToken);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "The Stripe reader payment '{PaymentIntentId}' couldn't be canceled.", context.Attempt.ProviderReference);
        }

        return PaymentCancelResult.Success();
    }

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
                IdempotencyKey = context.IdempotencyKey,
            });

            return refund.Status switch
            {
                "succeeded" => PaymentRefundResult.Success(refund.Id, StripeCurrency.FromMinorUnits(refund.Amount, refund.Currency), refund.Currency, context.GatewayMode),
                "failed" or "canceled" => PaymentRefundResult.Failed(refund.Status, refund.FailureReason),
                _ => PaymentRefundResult.Pending(refund.Id, context.GatewayMode),
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to refund the Stripe reader payment '{TransactionId}'.", context.OriginalTransactionId);

            return PaymentRefundResult.Failed("stripe_error", exception.Message);
        }
    }
}
