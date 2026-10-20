using System.Globalization;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// Cash handed over at a register. Its reference is the cash handed over, in the currency's minor units: it pays up to
/// what's due, and the rest is the change. The drawer's count is kept by the shift, not here.
/// </summary>
public sealed class PosCashTenderProvider : ICheckoutTenderProvider
{
    internal readonly IStringLocalizer S;

    public PosCashTenderProvider(IStringLocalizer<PosCashTenderProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Key
        => PosConstants.CashTenderKey;

    /// <summary>
    /// Returns the reference of an amount of cash handed over.
    /// </summary>
    public static string ToReference(decimal amount, string currency)
        => Money.ToMinorUnits(amount, currency).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the amount of cash a reference stands for, or <see langword="null"/> when it isn't one.
    /// </summary>
    public static decimal? FromReference(string reference, string currency)
        => long.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out var minor) && minor > 0
            ? minor / (decimal)Math.Pow(10, CurrencyScale.GetDecimalPlaces(currency))
            : null;

    public Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Only a register's sale takes cash; an online checkout told of some ignores it.
        if (!context.Flow.Session.TryGet<CheckoutCounterSale>(out _))
        {
            return Task.FromResult<IReadOnlyList<TenderLine>>([]);
        }

        var lines = context.References
            .Select(reference => (reference, amount: FromReference(reference, context.Invoice.Currency)))
            .Where(entry => entry.amount is > 0m)
            .Select(entry => new TenderLine
            {
                Reference = entry.reference,
                Description = S["Cash"],
                Amount = entry.amount.Value,
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TenderLine>>(lines);
    }

    public Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
        => Task.FromResult(FromReference(reference, currency));

    public Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
