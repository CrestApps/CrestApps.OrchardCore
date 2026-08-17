using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Builds a taxation-framework <see cref="TaxCalculationContext"/> from checkout domain data. It
/// translates checkout line items into <see cref="ITaxableItem"/> instances; it never calculates tax.
/// Keeping this in one place lets both the checkout flow and recurring billing produce identical,
/// deterministic contexts.
/// </summary>
public static class CheckoutTaxContextFactory
{
    /// <summary>
    /// Creates a context for the amounts on <paramref name="invoice"/> that are due now.
    /// </summary>
    /// <param name="invoice">The checkout invoice.</param>
    /// <param name="profile">The tax profile resolved for the checkout.</param>
    /// <param name="transactionDateUtc">The UTC date the tax is determined for.</param>
    public static TaxCalculationContext Create(CheckoutInvoice invoice, CheckoutTaxProfile profile, DateTime transactionDateUtc)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        profile ??= new CheckoutTaxProfile();

        var items = new List<ITaxableItem>();

        foreach (var lineItem in invoice.LineItems ?? [])
        {
            if (!IsDueNow(lineItem))
            {
                continue;
            }

            items.Add(CreateItem(lineItem, invoice.Currency, profile));
        }

        return new TaxCalculationContext
        {
            Items = items,
            Currency = invoice.Currency,
            TransactionDateUtc = transactionDateUtc,
            Origin = profile.Origin,
            Destination = profile.Destination,
            Customer = profile.Customer,
            DefaultPriceType = profile.PriceType,
        };
    }

    /// <summary>
    /// Creates a context for a recurring billing cycle from the amount the payment provider actually
    /// charged. The charged amount is authoritative and treated as tax-inclusive, so the taxation
    /// framework extracts the tax portion using the rules effective now (never over-charging or claiming
    /// uncollected tax). Each cycle therefore captures its own snapshot at the current rate while the
    /// destination and classification come from the current <paramref name="profile"/>.
    /// </summary>
    /// <param name="chargedAmount">The amount the provider charged for the cycle.</param>
    /// <param name="currency">The ISO-4217 currency the amount is expressed in.</param>
    /// <param name="profile">The tax profile resolved for the billing cycle.</param>
    /// <param name="transactionDateUtc">The UTC date the tax is determined for.</param>
    public static TaxCalculationContext CreateForRecurringCharge(
        decimal chargedAmount,
        string currency,
        CheckoutTaxProfile profile,
        DateTime transactionDateUtc)
    {
        profile ??= new CheckoutTaxProfile();

        var item = new TaxableItem
        {
            Id = "recurring-charge",
            Kind = TaxableItemKind.Service,
            Quantity = 1m,
            UnitPrice = chargedAmount,
            Currency = currency,
            TaxCategoryCode = profile.DefaultTaxCategoryCode,
            TaxClassificationCode = profile.DefaultTaxClassificationCode,

            // The amount charged already includes any applicable tax, so extract the portion rather than
            // adding tax on top of an amount the customer was already billed.
            PriceIncludesTax = true,
        };

        return new TaxCalculationContext
        {
            Items = [item],
            Currency = currency,
            TransactionDateUtc = transactionDateUtc,
            Origin = profile.Origin,
            Destination = profile.Destination,
            Customer = profile.Customer,
            DefaultPriceType = TaxPriceType.Inclusive,
        };
    }

    private static bool IsDueNow(CheckoutLineItem lineItem)
    {
        if (lineItem.Plan is null)
        {
            // One-off / initial amounts are always due now.
            return true;
        }

        // The first recurring charge is due now only when there is no start delay.
        return lineItem.Plan.StartDayDelay is null or 0;
    }

    private static TaxableItem CreateItem(CheckoutLineItem lineItem, string currency, CheckoutTaxProfile profile)
    {
        return new TaxableItem
        {
            Id = lineItem.ItemId,
            Kind = lineItem.Plan is null ? TaxableItemKind.Physical : TaxableItemKind.Service,
            Quantity = lineItem.Quantity,
            UnitPrice = (decimal)lineItem.UnitPrice,
            Currency = currency,
            TaxCategoryCode = profile.DefaultTaxCategoryCode,
            TaxClassificationCode = profile.DefaultTaxClassificationCode,
        };
    }
}
