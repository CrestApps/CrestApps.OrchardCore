using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The taxation-aware <see cref="ISubscriptionTaxService"/>. It builds a <see cref="TaxCalculationContext"/>
/// from the invoice line items that are due now and the flow's <see cref="SubscriptionTaxProfile"/>, then
/// delegates the actual determination to the taxation framework's <see cref="ITaxService"/>. The
/// subscription module never calculates tax itself and never persists a rate; it consumes the framework
/// and stores the immutable <see cref="TaxSnapshot"/> it returns.
/// </summary>
/// <remarks>
/// Tax is applied at two boundaries with deliberately different treatments, reflecting who controls the
/// charge:
/// <list type="bullet">
/// <item>
/// The initial checkout charge (<see cref="ApplyTaxAsync"/>) is fully controlled by the application, so
/// tax is determined on the exclusive amount due now and added on top, then folded into the up-front
/// charge. This covers one-time items and the first (non-delayed) subscription cycle.
/// </item>
/// <item>
/// Recurring renewals (<see cref="ApplyRecurringTaxAsync"/>) are driven by the payment provider, so the
/// application can only observe the amount the provider actually charged. That amount is treated as
/// tax-inclusive and the tax portion is extracted with the rules effective at billing time. Merchants
/// should therefore configure recurring provider prices as tax-inclusive for consistent collection.
/// </item>
/// </list>
/// Because the first cycle's tax is authoritatively determined at checkout and the provider's first
/// (<c>subscription_create</c>) invoice is not re-taxed by the renewal path, the first cycle is never
/// taxed twice.
/// </remarks>
public sealed class SubscriptionTaxService : ISubscriptionTaxService
{
    private readonly ITaxService _taxService;
    private readonly ITaxSnapshotFactory _snapshotFactory;
    private readonly ISubscriptionTaxProfileProvider _profileProvider;
    private readonly IClock _clock;

    public SubscriptionTaxService(
        ITaxService taxService,
        ITaxSnapshotFactory snapshotFactory,
        ISubscriptionTaxProfileProvider profileProvider,
        IClock clock)
    {
        _taxService = taxService;
        _snapshotFactory = snapshotFactory;
        _profileProvider = profileProvider;
        _clock = clock;
    }

    public async Task ApplyTaxAsync(Invoice invoice, SubscriptionFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(flow);

        var profile = await _profileProvider.GetProfileAsync(flow, cancellationToken);

        // Persist the resolved classification so recurring cycles reuse it when redetermining tax.
        invoice.TaxCategoryCode = profile.DefaultTaxCategoryCode;
        invoice.TaxClassificationCode = profile.DefaultTaxClassificationCode;

        var context = SubscriptionTaxContextFactory.Create(invoice, profile, _clock.UtcNow);

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

        var decimals = GetCurrencyDecimals(invoice.Currency);
        var roundedAddedTax = decimal.Round(addedTax, decimals, MidpointRounding.AwayFromZero);

        invoice.TaxAmount = (double)decimal.Round(result.TaxAmount, decimals, MidpointRounding.AwayFromZero);
        invoice.TaxLines = result.Lines;
        invoice.TaxSnapshot = _snapshotFactory.Create(context, result);
        invoice.GrandTotal = Math.Round(invoice.DueNow + (double)roundedAddedTax, decimals, MidpointRounding.AwayFromZero);

        // The up-front charge (PaymentIntent) collects the amount due now. Fold the exclusive tax into it
        // so the customer is actually charged the tax the checkout determined; otherwise tax would be
        // displayed but never collected. Tax already included in the price is not added again here.
        if (roundedAddedTax > 0m)
        {
            invoice.InitialPaymentAmount = Math.Round(
                (invoice.InitialPaymentAmount ?? 0d) + (double)roundedAddedTax,
                decimals,
                MidpointRounding.AwayFromZero);
        }
    }

    public async Task ApplyRecurringTaxAsync(PaymentInfo payment, ISubscriptionFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(session);

        var profile = await _profileProvider.GetProfileAsync(session, cancellationToken);

        // The amount the provider charged for this cycle is authoritative. It is redetermined with the
        // rules effective now and captured as an immutable snapshot on this payment; prior payments keep
        // their own historical snapshots. The charged amount is treated as tax-inclusive so tax is never
        // claimed beyond what the customer was actually billed.
        var context = SubscriptionTaxContextFactory.CreateForRecurringCharge(
            (decimal)payment.Amount,
            payment.Currency,
            profile,
            _clock.UtcNow);

        if (context.Items.Count == 0)
        {
            return;
        }

        var result = await _taxService.CalculateAsync(context, cancellationToken);

        var decimals = GetCurrencyDecimals(payment.Currency);

        payment.TaxAmount = (double)decimal.Round(result.TaxAmount, decimals, MidpointRounding.AwayFromZero);
        payment.TaxSnapshot = _snapshotFactory.Create(context, result);
    }

    private static int GetCurrencyDecimals(string currency)
        => string.IsNullOrEmpty(currency) ? 2 : StripeCurrency.GetDecimalPlaces(currency);
}