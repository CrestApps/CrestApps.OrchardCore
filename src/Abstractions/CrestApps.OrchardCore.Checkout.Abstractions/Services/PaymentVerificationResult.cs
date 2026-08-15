using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The result of <see cref="ICheckoutPaymentProvider.VerifyAsync"/>, describing the provider's
/// authoritative view of an attempt.
/// </summary>
public sealed class PaymentVerificationResult
{
    /// <summary>
    /// The authoritative status reported by the provider.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// The provider transaction id for a settled payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Whether <see cref="Amount"/> and <see cref="Currency"/> are the provider's authoritative record of
    /// what was charged. Providers that actually move money (for example Stripe) set this to <c>true</c>,
    /// which makes the checkout validate the charged amount and currency against the attempt before it
    /// settles the obligation. Deferred providers that never touch a processor (for example Pay Later) set
    /// this to <c>false</c>, so the checkout accepts the confirmation without an amount cross-check.
    /// </summary>
    public bool ReportsAuthoritativeAmount { get; set; }

    /// <summary>
    /// The amount the provider actually charged, in the invoice currency.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// The tax the provider actually collected, when it collects tax dynamically.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// The immutable tax determination the provider (or checkout) captured for the charge.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// The ISO-4217 currency the provider charged in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The provider mode the charge ran in (for example test or live).
    /// </summary>
    public GatewayMode GatewayMode { get; set; }
}
