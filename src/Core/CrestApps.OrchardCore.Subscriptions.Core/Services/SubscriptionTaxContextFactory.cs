using System;
using System.Collections.Generic;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Builds a taxation-framework <see cref="TaxCalculationContext"/> from subscription domain data. It
/// translates invoice line items into <see cref="ITaxableItem"/> instances; it never calculates tax.
/// Keeping this in one place lets both the checkout flow and recurring billing produce identical,
/// deterministic contexts.
/// </summary>
public static class SubscriptionTaxContextFactory
{
    /// <summary>
    /// Creates a context for the amounts on <paramref name="invoice"/> that are due now.
    /// </summary>
    public static TaxCalculationContext Create(Invoice invoice, SubscriptionTaxProfile profile, DateTime transactionDateUtc)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        profile ??= new SubscriptionTaxProfile();

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
    /// Creates a context for a single recurring billing charge. The amount reflects the current billing
    /// event so that the tax is redetermined with the rules effective at billing time.
    /// </summary>
    public static TaxCalculationContext CreateForRecurringCharge(
        InvoiceLineItem lineItem,
        string currency,
        SubscriptionTaxProfile profile,
        DateTime transactionDateUtc)
    {
        ArgumentNullException.ThrowIfNull(lineItem);

        profile ??= new SubscriptionTaxProfile();

        return new TaxCalculationContext
        {
            Items = [CreateItem(lineItem, currency, profile)],
            Currency = currency,
            TransactionDateUtc = transactionDateUtc,
            Origin = profile.Origin,
            Destination = profile.Destination,
            Customer = profile.Customer,
            DefaultPriceType = profile.PriceType,
        };
    }

    private static bool IsDueNow(InvoiceLineItem lineItem)
    {
        if (lineItem.Subscription is null)
        {
            // One-off / initial amounts are always due now.
            return true;
        }

        // The first subscription charge is due now only when there is no start delay.
        return lineItem.Subscription.SubscriptionDayDelay is null or 0;
    }

    private static TaxableItem CreateItem(InvoiceLineItem lineItem, string currency, SubscriptionTaxProfile profile)
    {
        return new TaxableItem
        {
            Id = lineItem.Id,
            Kind = lineItem.Subscription is null ? TaxableItemKind.Physical : TaxableItemKind.Service,
            Quantity = lineItem.Quantity,
            UnitPrice = (decimal)lineItem.UnitPrice,
            Currency = currency,
            TaxCategoryCode = profile.DefaultTaxCategoryCode,
            TaxClassificationCode = profile.DefaultTaxClassificationCode,
        };
    }
}
