using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// A card taken on the store's own card terminal, beside the register. The terminal has already charged the card when
/// staff record it, so the payment is confirmed at once. It's offered only for a register's sale, to staff allowed to
/// sell there.
/// </summary>
public sealed class PosCounterCardPaymentProvider : ICheckoutPaymentProvider, ICheckoutPaymentProviderAvailability, ICheckoutPaymentProviderDescription, ICheckoutPaymentRefundProvider
{
    private const string ReferencePrefix = "pos_card_";
    private const string RefundPrefix = "pos_card_re_";

    internal readonly IStringLocalizer S;

    public PosCounterCardPaymentProvider(IStringLocalizer<PosCounterCardPaymentProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Key
        => PosConstants.CounterCardProviderKey;

    public string DisplayName
        => S["Card on the counter terminal"];

    public string Description
        => S["Taken on the store's card terminal; record it once the terminal has approved it."];

    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRefunds = true,
    };

    public Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
        => Task.FromResult(session is not null && session.TryGet<CheckoutCounterSale>(out _));

    public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Session is not { } session || !session.TryGet<CheckoutCounterSale>(out _))
        {
            return Task.FromResult(PaymentBeginResult.Failure(S["A counter card payment is only for a sale rung up at a register."]));
        }

        // The terminal's approval code, when staff give it, is the payment's reference.
        var reference = string.IsNullOrEmpty(context.Attempt.ProviderReference)
            ? ReferencePrefix + (context.ProviderData is not null && context.ProviderData.TryGetValue("approvalCode", out var code) && !string.IsNullOrWhiteSpace(code) ? code.Trim() : IdGenerator.GenerateId())
            : context.Attempt.ProviderReference;

        return Task.FromResult(PaymentBeginResult.Success(reference));
    }

    public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attempt = context.Attempt;

        if (string.IsNullOrEmpty(attempt.ProviderReference))
        {
            return Task.FromResult(new PaymentVerificationResult { Status = PaymentStatus.Unknown });
        }

        return Task.FromResult(new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            ReportsAuthoritativeAmount = true,
            TransactionId = attempt.ProviderReference,
            Amount = attempt.ExpectedAmount,
            TaxAmount = attempt.ExpectedTaxAmount,
            TaxSnapshot = attempt.TaxSnapshot,
            Currency = attempt.Currency,
            GatewayMode = GatewayMode.Live,
        });
    }

    public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentCancelResult.Success());

    // The money goes back on the terminal; this records that it did.
    public Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reference = RefundPrefix + (string.IsNullOrEmpty(context.IdempotencyKey) ? IdGenerator.GenerateId() : context.IdempotencyKey);

        return Task.FromResult(PaymentRefundResult.Success(reference, context.Amount, context.Currency, GatewayMode.Live));
    }
}
