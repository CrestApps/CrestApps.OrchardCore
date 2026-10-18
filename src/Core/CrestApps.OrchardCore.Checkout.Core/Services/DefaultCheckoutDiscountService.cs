using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutDiscountService"/>. It collects what every registered provider offers
/// and applies the arithmetic itself.
/// </summary>
/// <remarks>
/// Providers decide what to take off; this decides how. Centralizing that is what makes three rules hold no
/// matter how many providers a site installs:
///
/// <list type="bullet">
/// <item><description>A total can never go below zero. A coupon worth more than the basket makes the
/// purchase free, not a payment the site owes the customer.</description></item>
/// <item><description>Every amount is rounded at the invoice currency's own precision, so a discounted
/// total still matches to the cent what the gateway settles.</description></item>
/// <item><description>Discounts land before tax, so the customer is not taxed on money they did not
/// pay.</description></item>
/// </list>
/// </remarks>
public sealed class DefaultCheckoutDiscountService : ICheckoutDiscountService
{
    private readonly IEnumerable<ICheckoutDiscountProvider> _providers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutDiscountService"/> class.
    /// </summary>
    /// <param name="providers">The registered discount providers.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCheckoutDiscountService(
        IEnumerable<ICheckoutDiscountProvider> providers,
        ILogger<DefaultCheckoutDiscountService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ApplyDiscountsAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        invoice.Discounts ??= [];
        invoice.Discounts.Clear();

        // A checkout that pays off an existing balance (an outstanding transaction) has no price of its own to
        // reduce. Unless the module that settles the balance takes what the discounts take off off that balance
        // (and redeems the codes once it is settled), a discount would only leave the balance partly unpaid and
        // still use up the code, so nothing is offered, whatever a provider would have returned.
        if (flow?.Session is CheckoutSession { SettlesExistingBalance: true, AcceptsDiscountsOnBalance: false })
        {
            return;
        }

        var context = new CheckoutDiscountContext(invoice, flow);

        foreach (var provider in _providers)
        {
            IReadOnlyList<DiscountLine> discounts;

            try
            {
                discounts = await provider.GetDiscountsAsync(context, cancellationToken);
            }
            catch (Exception exception)
            {
                // A discount provider that throws must not stop the customer buying. The purchase proceeds
                // at full price, which is recoverable; a failed checkout is not.
                _logger.LogError(exception, "The discount provider '{ProviderKey}' failed. The checkout continues without its discounts.", provider.Key);

                continue;
            }

            foreach (var discount in discounts ?? [])
            {
                if (discount is null || discount.Amount <= 0m)
                {
                    continue;
                }

                discount.ProviderKey ??= provider.Key;

                invoice.Discounts.Add(discount);
            }
        }

        if (invoice.Discounts.Count == 0)
        {
            return;
        }

        Apply(invoice);
    }

    private static void Apply(CheckoutInvoice invoice)
    {
        var currency = invoice.Currency;

        // Clamping each bucket separately, rather than the grand total, is what keeps a large one-time
        // coupon from eating into a recurring charge the customer did agree to pay every month. Within a bucket a
        // discount takes off only what it can discount: shipping, for one, stays whole unless a discount is aimed at
        // it, and both come out of the amount paid now.
        var oneTimeApplied = ClampAndApply(invoice.InitialPaymentAmount, CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.OneTime), Sum(invoice, DiscountTarget.OneTime), currency, out var newInitial);
        var shippingApplied = ClampAndApply(newInitial, CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.Shipping), Sum(invoice, DiscountTarget.Shipping), currency, out newInitial);
        var firstCycleApplied = ClampAndApply(invoice.FirstRecurringPaymentAmount, CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.FirstCycle), Sum(invoice, DiscountTarget.FirstCycle), currency, out var newFirstCycle);

        invoice.InitialPaymentAmount = newInitial;
        invoice.FirstRecurringPaymentAmount = newFirstCycle;
        invoice.DueNow = Money.Round(Math.Max(0m, invoice.DueNow - oneTimeApplied - shippingApplied - firstCycleApplied), currency);

        // What is recorded has to be what was actually taken off, or a receipt shows a discount larger than
        // the price it was applied to.
        Rewrite(invoice, DiscountTarget.OneTime, oneTimeApplied, currency);
        Rewrite(invoice, DiscountTarget.Shipping, shippingApplied, currency);
        Rewrite(invoice, DiscountTarget.FirstCycle, firstCycleApplied, currency);

        // Each line knows its share of every discount, so it is taxed on what the customer actually pays for it.
        foreach (var discount in invoice.Discounts)
        {
            Allocate(invoice, discount, currency);
        }

        for (var i = invoice.Discounts.Count - 1; i >= 0; i--)
        {
            if (invoice.Discounts[i].Amount <= 0m)
            {
                invoice.Discounts.RemoveAt(i);
            }
        }
    }

    private static decimal Sum(CheckoutInvoice invoice, DiscountTarget target)
        => invoice.Discounts.Where(discount => discount.Target == target).Sum(discount => discount.Amount);

    private static decimal ClampAndApply(decimal? amount, decimal cap, decimal discount, string currency, out decimal? result)
    {
        if (amount is null || discount <= 0m)
        {
            result = amount;

            return 0m;
        }

        var applied = Money.Round(Math.Max(0m, Math.Min(Math.Min(amount.Value, cap), discount)), currency);

        result = Money.Round(amount.Value - applied, currency);

        return applied;
    }

    // Spreads what a discount took off across the lines it can discount, in proportion to each line's total; the
    // last line takes what rounding leaves, so the shares add up to the discount exactly. A line another discount
    // already took from has less room, and what it can't take goes to the lines that still have some.
    private static void Allocate(CheckoutInvoice invoice, DiscountLine discount, string currency)
    {
        if (discount.Amount <= 0m || invoice.LineItems is not { Length: > 0 } lines)
        {
            return;
        }

        var eligible = lines
            .Where(line => CheckoutDiscountBase.IsDiscountable(line, discount.Target) && discount.IsLimitedTo(line) && line.GetLineTotal(currency) > 0m)
            .ToArray();
        var total = eligible.Sum(line => line.GetLineTotal(currency));

        if (total <= 0m)
        {
            return;
        }

        var remaining = discount.Amount;

        for (var i = 0; i < eligible.Length; i++)
        {
            var lineTotal = eligible[i].GetLineTotal(currency);
            var share = i == eligible.Length - 1
                ? remaining
                : Money.Round(discount.Amount * (lineTotal / total), currency);

            share = Math.Max(0m, Math.Min(Math.Min(share, remaining), Room(eligible[i], currency)));

            eligible[i].DiscountAmount += share;
            remaining -= share;
        }

        foreach (var line in eligible)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var extra = Math.Min(remaining, Room(line, currency));

            line.DiscountAmount += extra;
            remaining -= extra;
        }
    }

    private static decimal Room(CheckoutLineItem line, string currency)
        => Math.Max(0m, line.GetLineTotal(currency) - line.DiscountAmount);

    // Scales the recorded lines down proportionally when the bucket could not absorb the whole discount, so
    // the sum of what is shown equals what was taken off.
    private static void Rewrite(CheckoutInvoice invoice, DiscountTarget target, decimal applied, string currency)
    {
        var lines = invoice.Discounts.Where(discount => discount.Target == target).ToArray();

        if (lines.Length == 0)
        {
            return;
        }

        var requested = lines.Sum(line => line.Amount);

        if (requested <= applied)
        {
            return;
        }

        var remaining = applied;

        for (var i = 0; i < lines.Length; i++)
        {
            // The last line takes whatever is left, so rounding never loses or invents a cent.
            var share = i == lines.Length - 1
                ? remaining
                : Money.Round(applied * (lines[i].Amount / requested), currency);

            share = Math.Max(0m, Math.Min(share, remaining));

            lines[i].Amount = share;
            remaining -= share;
        }
    }
}
