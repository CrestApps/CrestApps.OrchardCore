using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Builds the authoritative invoice for a checkout from the billing items its steps contributed.
/// </summary>
/// <remarks>
/// The invoice is rebuilt rather than patched whenever something that changes the price changes, because a
/// total assembled by adjusting a previous total drifts: apply a coupon, remove it, apply another, and the
/// arithmetic no longer matches the line items the customer is looking at.
/// </remarks>
public sealed class CheckoutInvoiceBuilder
{
    private readonly ISiteService _siteService;
    private readonly ICheckoutDiscountService _discountService;
    private readonly ICheckoutTaxService _taxService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutInvoiceBuilder"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the fallback currency.</param>
    /// <param name="discountService">The discount service applied before tax.</param>
    /// <param name="taxService">The tax service, authoritative for what tax is owed.</param>
    public CheckoutInvoiceBuilder(
        ISiteService siteService,
        ICheckoutDiscountService discountService,
        ICheckoutTaxService taxService)
    {
        _siteService = siteService;
        _discountService = discountService;
        _taxService = taxService;
    }

    /// <summary>
    /// Rebuilds the checkout's invoice and stores it on the session.
    /// </summary>
    /// <param name="flow">The checkout flow.</param>
    public async Task BuildAsync(CheckoutFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        
        var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();

        // What is being bought decides the currency; the site setting is only the fallback for a checkout
        // that did not name one. Building the invoice in the site currency while the line items are priced
        // in another would charge the customer a number that belongs to a different currency.
        var currency = string.IsNullOrEmpty(flow.Session.Currency)
            ? settings.Currency
            : flow.Session.Currency;

        var invoice = new CheckoutInvoice
        {
            Currency = currency,
        };

        var lineItems = new List<CheckoutLineItem>();

        // Every step is billed, including the ones that are never drawn. A step is concealed because there is
        // nothing to ask the customer, not because it is free: the plan they already chose and a fee that
        // needs no input both carry charges. Building the invoice from the visible steps alone would drop
        // those charges and complete the checkout for nothing.
        foreach (var step in flow.Session.Steps.OrderBy(step => step.Order))
        {
            if (step.BillingItems == null)
            {
                continue;
            }

            foreach (var billingItem in step.BillingItems)
            {
                var lineItem = new CheckoutLineItem
                {
                    ItemId = billingItem.ItemId,
                    Description = billingItem.Description,
                    Quantity = 1,
                    UnitPrice = billingItem.Amount,
                    Plan = billingItem.Plan,
                };

                if (billingItem.Plan == null)
                {
                    invoice.InitialPaymentAmount ??= 0;
                    invoice.InitialPaymentAmount += lineItem.GetLineTotal(currency);
                    invoice.DueNow += lineItem.GetLineTotal(currency);
                }
                else if (CheckoutObligations.GetDeferralDays([lineItem]) == 0)
                {
                    // A trial or a delayed start collects nothing now: the agreement is established with a
                    // payment method attached and the gateway bills on the later date. Counting it as due now
                    // would show the customer a charge nobody is about to take.
                    invoice.FirstRecurringPaymentAmount ??= 0;
                    invoice.FirstRecurringPaymentAmount += lineItem.GetLineTotal(currency);
                    invoice.DueNow += lineItem.GetLineTotal(currency);
                }

                lineItems.Add(lineItem);
            }
        }

        invoice.LineItems = lineItems.ToArray();
        invoice.Subtotals = lineItems.Where(x => x.Plan != null)
            .GroupBy(x => new BillingDurationKey(x.Plan.DurationType, x.Plan.BillingDuration))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.UnitPrice * y.Quantity));

        // Round every amount at the invoice currency's own precision so the expected amounts match what the
        // gateway actually settles. Rounding a zero-decimal currency (for example JPY) to two decimals would
        // otherwise cause a valid payment to be rejected during verification.
        if (invoice.InitialPaymentAmount.HasValue)
        {
            invoice.InitialPaymentAmount = Money.Round(invoice.InitialPaymentAmount.Value, currency);
        }

        if (invoice.FirstRecurringPaymentAmount.HasValue)
        {
            invoice.FirstRecurringPaymentAmount = Money.Round(invoice.FirstRecurringPaymentAmount.Value, currency);
        }

        invoice.DueNow = Money.Round(invoice.DueNow, currency);

        // Discounts land before tax, always. Taxing the full price and then discounting the total charges the
        // customer tax on money they never paid, which is wrong for them and wrong on the return the site
        // owner files.
        await _discountService.ApplyDiscountsAsync(invoice, flow);

        // Taxation is authoritative. When the Taxation feature is disabled this is a no-op that sets the
        // grand total to the amount due now; otherwise it determines the tax, records the tax lines, folds
        // exclusive tax into the up-front charge, and captures an immutable snapshot on the invoice.
        await _taxService.ApplyTaxAsync(invoice, flow);

        flow.Session.Put(invoice);
    
    }
}
