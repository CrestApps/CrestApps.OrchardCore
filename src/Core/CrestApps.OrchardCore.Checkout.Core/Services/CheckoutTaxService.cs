using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The taxation-aware <see cref="ICheckoutTaxService"/>. It builds a
/// <see cref="Taxation.Models.TaxCalculationContext"/> from the invoice line items that are due now and the
/// checkout's <see cref="CheckoutTaxProfile"/>, then delegates the actual determination to the taxation
/// framework's <see cref="ITaxService"/>. The checkout never calculates tax itself and never persists a
/// rate; it consumes the framework and stores the immutable <see cref="Taxation.Models.TaxSnapshot"/> it
/// returns.
/// </summary>
/// <remarks>
/// Tax is applied at two boundaries with deliberately different treatments, reflecting who controls the
/// charge. The initial checkout charge is fully controlled by the application, so exclusive tax is
/// determined on the amount due now and folded into the up-front charge. Recurring renewals are driven by
/// the payment provider, so the amount the provider charged is treated as tax-inclusive and the tax
/// portion is extracted with the rules effective at billing time.
/// </remarks>
public sealed class CheckoutTaxService : ICheckoutTaxService
{
    private readonly ITaxService _taxService;
    private readonly ITaxSnapshotFactory _snapshotFactory;
    private readonly ICheckoutTaxProfileProvider _profileProvider;
    private readonly IClock _clock;

    public CheckoutTaxService(
        ITaxService taxService,
        ITaxSnapshotFactory snapshotFactory,
        ICheckoutTaxProfileProvider profileProvider,
        IClock clock)
    {
        _taxService = taxService;
        _snapshotFactory = snapshotFactory;
        _profileProvider = profileProvider;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(flow);

        var profile = await _profileProvider.GetProfileAsync(flow, cancellationToken);

        // Persist the resolved classification so recurring cycles reuse it when redetermining tax.
        invoice.TaxCategoryCode = profile.DefaultTaxCategoryCode;
        invoice.TaxClassificationCode = profile.DefaultTaxClassificationCode;

        var context = CheckoutTaxContextFactory.Create(invoice, profile, _clock.UtcNow);

        if (context.Items.Count == 0)
        {
            invoice.TaxAmount = 0;
            invoice.TaxLines = null;
            invoice.TaxSnapshot = null;
            invoice.GrandTotal = invoice.DueNow;

            return;
        }

        var result = await _taxService.CalculateAsync(context, cancellationToken);

        // Only tax that is not already included in the price is added on top of the amount due now.
        var addedTax = result.Lines
            .Where(line => !line.IncludedInPrice)
            .Sum(line => line.TaxAmount);

        var decimals = CurrencyScale.GetDecimalPlaces(invoice.Currency);
        var roundedAddedTax = decimal.Round(addedTax, decimals, MidpointRounding.AwayFromZero);

        invoice.TaxAmount = (double)decimal.Round(result.TaxAmount, decimals, MidpointRounding.AwayFromZero);
        invoice.TaxLines = result.Lines;
        invoice.TaxSnapshot = _snapshotFactory.Create(context, result);
        invoice.GrandTotal = Math.Round(invoice.DueNow + (double)roundedAddedTax, decimals, MidpointRounding.AwayFromZero);

        // The up-front charge collects the amount due now. Fold the exclusive tax into it so the customer
        // is actually charged the tax the checkout determined; otherwise tax would be displayed but never
        // collected. Tax already included in the price is not added again here.
        if (roundedAddedTax > 0m)
        {
            invoice.InitialPaymentAmount = Math.Round(
                (invoice.InitialPaymentAmount ?? 0d) + (double)roundedAddedTax,
                decimals,
                MidpointRounding.AwayFromZero);
        }
    }

    /// <inheritdoc/>
    public async Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(session);

        var profile = await _profileProvider.GetProfileAsync(session, cancellationToken);

        // The amount the provider charged for this cycle is authoritative. It is redetermined with the
        // rules effective now and captured as an immutable snapshot on this payment; prior payments keep
        // their own historical snapshots. The charged amount is treated as tax-inclusive so tax is never
        // claimed beyond what the customer was actually billed.
        var context = CheckoutTaxContextFactory.CreateForRecurringCharge(
            (decimal)payment.Amount,
            payment.Currency,
            profile,
            _clock.UtcNow);

        if (context.Items.Count == 0)
        {
            return;
        }

        var result = await _taxService.CalculateAsync(context, cancellationToken);

        var decimals = CurrencyScale.GetDecimalPlaces(payment.Currency);

        payment.TaxAmount = (double)decimal.Round(result.TaxAmount, decimals, MidpointRounding.AwayFromZero);
        payment.TaxSnapshot = _snapshotFactory.Create(context, result);
    }
}
