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

        invoice.TaxAmount = (double)decimal.Round(result.TaxAmount, decimals, MidpointRounding.AwayFromZero);
        invoice.TaxLines = result.Lines;
        invoice.TaxSnapshot = _snapshotFactory.Create(context, result);
        invoice.GrandTotal = Math.Round(invoice.DueNow + (double)addedTax, decimals, MidpointRounding.AwayFromZero);
    }

    private static int GetCurrencyDecimals(string currency)
        => string.IsNullOrEmpty(currency) ? 2 : StripeCurrency.GetDecimalPlaces(currency);
}
