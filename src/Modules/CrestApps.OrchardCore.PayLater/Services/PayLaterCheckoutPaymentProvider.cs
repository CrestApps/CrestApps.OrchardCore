using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// A deferred <see cref="ICheckoutPaymentProvider"/> that records an offline "pay later" commitment
/// instead of moving money through an external processor. Because it never touches a gateway, its
/// verification reports that it is <em>not</em> the authoritative source of a charged amount, which tells
/// the checkout to accept the commitment without cross-checking a processor's charged total while still
/// requiring a recorded transaction id. This keeps the same reconciliation guarantees as a real gateway
/// without ever fabricating a "paid" record that a processor would contradict.
/// </summary>
public sealed class PayLaterCheckoutPaymentProvider : ICheckoutPaymentProvider
{
    /// <summary>
    /// The stable processor key that identifies the Pay Later provider.
    /// </summary>
    public const string ProcessorKey = "pay-later";

    private readonly IHostEnvironment _hostEnvironment;

    internal readonly IStringLocalizer S;

    public PayLaterCheckoutPaymentProvider(
        IHostEnvironment hostEnvironment,
        IStringLocalizer<PayLaterCheckoutPaymentProvider> stringLocalizer)
    {
        _hostEnvironment = hostEnvironment;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key => ProcessorKey;

    /// <inheritdoc/>
    public string DisplayName => S["Pay Later"];

    /// <inheritdoc/>
    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = true,
        SupportsHostedCheckout = false,
        SupportsEmbeddedElements = false,
        SupportsCombinedOneTimeAndRecurring = true,
        CollectsTaxDynamically = false,
        SupportsRefunds = false,
    };

    /// <inheritdoc/>
    public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // There is no external resource to create for an offline commitment, so a locally generated
        // reference is the provider's authoritative reference for the attempt.
        var reference = string.IsNullOrEmpty(context.Attempt?.ProviderReference)
            ? IdGenerator.GenerateId()
            : context.Attempt.ProviderReference;

        return Task.FromResult(PaymentBeginResult.Success(reference));
    }

    /// <inheritdoc/>
    public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Attempt);

        // A non-production deployment records its offline commitments as test data, mirroring how a real
        // gateway reports its mode, instead of always reporting Live.
        var gatewayMode = _hostEnvironment.IsProduction() ? GatewayMode.Live : GatewayMode.Testing;

        // The commitment is only confirmable once it has been begun and its reference persisted. If the
        // attempt was created but never begun (for example a crash between persisting the attempt and
        // calling BeginAsync), there is nothing to confirm yet, so report Unknown and leave the obligation
        // outstanding rather than fabricating a settlement from the attempt id.
        if (string.IsNullOrEmpty(context.Attempt.ProviderReference))
        {
            return Task.FromResult(new PaymentVerificationResult
            {
                Status = PaymentStatus.Unknown,
            });
        }

        return Task.FromResult(new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,

            // Pay Later never moves money at a processor, so it is not the authoritative source of a
            // charged amount. The reconciliation service therefore records the commitment on the strength
            // of the transaction id alone, without an amount cross-check.
            ReportsAuthoritativeAmount = false,
            TransactionId = context.Attempt.ProviderReference,
            Amount = context.Attempt.ExpectedAmount,
            TaxAmount = context.Attempt.ExpectedTaxAmount,
            Currency = context.Attempt.Currency,
            GatewayMode = gatewayMode,
        });
    }

    /// <inheritdoc/>
    public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentCancelResult.Success());
}
