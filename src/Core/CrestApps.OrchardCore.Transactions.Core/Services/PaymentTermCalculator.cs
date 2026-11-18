using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="IPaymentTermCalculator"/>. It works on local dates only and holds no state.
/// </summary>
public sealed class PaymentTermCalculator : IPaymentTermCalculator
{
    /// <inheritdoc/>
    public DateOnly GetDueDate(PaymentTerm term, DateOnly issueDate)
    {
        if (term is null)
        {
            return issueDate;
        }

        var due = term.Kind switch
        {
            PaymentTermKind.NetDays => issueDate.AddDays(term.Days),
            PaymentTermKind.EndOfMonth => EndOfMonth(issueDate).AddDays(term.Days),
            PaymentTermKind.DayOfNextMonth => DayOfNextMonth(issueDate, term.DayOfMonth),
            _ => issueDate,
        };

        return due < issueDate ? issueDate : due;
    }

    /// <inheritdoc/>
    public EarlyPaymentDiscount? GetEarlyPaymentDiscount(PaymentTerm term, DateOnly issueDate, decimal amount, string currency)
    {
        if (term?.DiscountPercent is not > 0m || term.DiscountDays is not > 0 || amount <= 0m)
        {
            return null;
        }

        var discount = CurrencyScale.Round(amount * term.DiscountPercent.Value / 100m, currency);

        return discount <= 0m
            ? null
            : new EarlyPaymentDiscount(issueDate.AddDays(term.DiscountDays.Value), discount);
    }

    private static DateOnly EndOfMonth(DateOnly date)
        => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static DateOnly DayOfNextMonth(DateOnly date, int dayOfMonth)
    {
        var next = new DateOnly(date.Year, date.Month, 1).AddMonths(1);
        var day = Math.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(next.Year, next.Month));

        return new DateOnly(next.Year, next.Month, day);
    }
}
