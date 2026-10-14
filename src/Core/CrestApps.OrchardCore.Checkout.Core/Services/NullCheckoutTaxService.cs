using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The no-op <see cref="ICheckoutTaxService"/> used when the Taxation feature is disabled. It leaves the
/// invoice untaxed so checkout keeps working normally, and is replaced by a taxation-aware implementation
/// when the Taxation feature is enabled.
/// </summary>
public sealed class NullCheckoutTaxService : ICheckoutTaxService
{
    /// <inheritdoc/>
    public Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.TaxAmount = 0;
        invoice.GrandTotal = invoice.DueNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        payment.TaxAmount = 0;

        return Task.CompletedTask;
    }
}
