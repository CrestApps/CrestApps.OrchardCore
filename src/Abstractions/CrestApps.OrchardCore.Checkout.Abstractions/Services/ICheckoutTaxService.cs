using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Applies taxation to a checkout <see cref="CheckoutInvoice"/>. The checkout framework never calculates
/// tax itself; it consumes the taxation framework through this seam. When the Taxation feature is disabled
/// the registered implementation is a no-op so checkout keeps working normally.
/// </summary>
public interface ICheckoutTaxService
{
    /// <summary>
    /// Determines the tax for the amounts due now on the supplied invoice, sets
    /// <see cref="CheckoutInvoice.TaxAmount"/>, <see cref="CheckoutInvoice.TaxLines"/>,
    /// <see cref="CheckoutInvoice.TaxSnapshot"/>, and recomputes <see cref="CheckoutInvoice.GrandTotal"/>.
    /// Any exclusive tax is also folded into <see cref="CheckoutInvoice.InitialPaymentAmount"/> so the
    /// up-front charge actually collects the tax the checkout determined.
    /// </summary>
    /// <param name="invoice">The invoice to tax. Its <see cref="CheckoutInvoice.DueNow"/> is expected to be set.</param>
    /// <param name="flow">The checkout flow that describes the transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the tax for a single recurring billing cycle and records it on the supplied
    /// <paramref name="payment"/> (<see cref="PaymentRecord.TaxAmount"/> and an immutable
    /// <see cref="PaymentRecord.TaxSnapshot"/>). The tax is recalculated with the rules effective now so
    /// each cycle carries its own snapshot; previous payments are never altered.
    /// </summary>
    /// <param name="payment">The recurring payment to record tax on.</param>
    /// <param name="session">The persisted checkout session that holds the checkout invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default);
}
