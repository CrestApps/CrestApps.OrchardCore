using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Hosting;
using OrchardCore;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// A payment method that takes no money at checkout: the buyer commits to pay later, by bank transfer or on
/// delivery, and the balance is recorded as an outstanding transaction the store settles when the money arrives.
/// </summary>
/// <remarks>
/// It never touches a gateway, so its verification reports that it is not the authoritative source of a charged
/// amount: the checkout accepts the commitment on the strength of its reference, without a processor's total to
/// cross-check, and never fabricates a settlement.
/// </remarks>
public abstract class OfflineCheckoutPaymentProvider : ICheckoutPaymentProvider, ICheckoutPaymentProviderAvailability, ICheckoutPaymentProviderDescription
{
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineCheckoutPaymentProvider"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The host environment, which decides whether commitments are test data.</param>
    protected OfflineCheckoutPaymentProvider(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc/>
    public abstract string Key { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public virtual PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = false,
        SupportsHostedCheckout = false,
        SupportsEmbeddedElements = false,
        SupportsCombinedOneTimeAndRecurring = false,
        CollectsTaxDynamically = false,
        SupportsRefunds = false,

        // A commitment to pay later collects nothing, so it cannot pay off a balance that already exists.
        DefersPayment = true,
    };

    /// <summary>
    /// Returns when the balance a checkout leaves is due, or <see langword="null"/> for no due date.
    /// </summary>
    /// <param name="now">The time the checkout completed.</param>
    public abstract Task<DateTime?> GetDueUtcAsync(DateTime now);

    /// <inheritdoc/>
    public abstract Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // There is no external resource to create for an offline commitment, so a locally generated reference is
        // the provider's authoritative reference for the attempt.
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

        // An attempt created but never begun (a crash between the two) has nothing to confirm yet, so the obligation
        // stays outstanding rather than being settled from the attempt id.
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
            ReportsAuthoritativeAmount = false,
            TransactionId = context.Attempt.ProviderReference,
            Amount = context.Attempt.ExpectedAmount,
            TaxAmount = context.Attempt.ExpectedTaxAmount,
            Currency = context.Attempt.Currency,

            // A non-production deployment records its offline commitments as test data, the way a gateway reports
            // its mode.
            GatewayMode = _hostEnvironment.IsProduction() ? GatewayMode.Live : GatewayMode.Testing,
        });
    }

    /// <inheritdoc/>
    public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentCancelResult.Success());
}
