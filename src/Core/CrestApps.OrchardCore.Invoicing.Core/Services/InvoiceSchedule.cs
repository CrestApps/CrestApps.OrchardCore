using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The rules of an invoice's payment schedule: each part has a due date and either an amount or a percent; parts are all
/// by amount (adding up to the total) or all by percent (adding up to 100); they fall due from the issue date on, each after
/// the one before.
/// </summary>
public static class InvoiceSchedule
{
    /// <summary>
    /// Works out what each part comes to for the invoice's total. Returns the amounts, or the reasons the schedule can't be
    /// used.
    /// </summary>
    public static (IReadOnlyList<decimal> Amounts, IReadOnlyList<string> Errors) Resolve(Invoice invoice, DateOnly issueDate, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var parts = invoice.Schedule ?? [];
        var errors = new List<string>();
        var total = invoice.Totals?.Total ?? 0m;
        var currency = invoice.Currency;

        if (parts.Count == 0)
        {
            return ([], errors);
        }

        if (parts.Any(part => part.Amount.HasValue == part.Percent.HasValue))
        {
            errors.Add(S["Give each part of the payment schedule either an amount or a percent."]);
        }
        else if (parts.Select(part => part.Percent.HasValue).Distinct().Count() > 1)
        {
            errors.Add(S["Set the parts of the payment schedule all by amount or all by percent."]);
        }

        if (parts.Any(part => part.Amount is <= 0m || part.Percent is <= 0m))
        {
            errors.Add(S["Every part of the payment schedule must be more than zero."]);
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].DueDate < issueDate || i > 0 && parts[i].DueDate <= parts[i - 1].DueDate)
            {
                errors.Add(S["The parts of the payment schedule must fall due from the issue date on, each after the one before."]);

                break;
            }
        }

        if (errors.Count > 0)
        {
            return ([], errors);
        }

        var amounts = new List<decimal>();

        if (parts[0].Percent.HasValue)
        {
            if (parts.Sum(part => part.Percent.Value) != 100m)
            {
                errors.Add(S["The percents of the payment schedule must add up to 100."]);

                return ([], errors);
            }

            // Each part is its share rounded to the currency; the last takes what rounding left.
            for (var i = 0; i < parts.Count - 1; i++)
            {
                amounts.Add(CurrencyScale.Round(total * parts[i].Percent.Value / 100m, currency));
            }

            amounts.Add(total - amounts.Sum());
        }
        else
        {
            amounts.AddRange(parts.Select(part => CurrencyScale.Round(part.Amount.Value, currency)));

            if (amounts.Sum() != total)
            {
                errors.Add(S["The payment schedule adds up to {0}, but the invoice totals {1}.", amounts.Sum().ToString("0.00", System.Globalization.CultureInfo.CurrentCulture), total.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture)]);

                return ([], errors);
            }
        }

        if (amounts.Any(amount => amount <= 0m))
        {
            errors.Add(S["The invoice is too small to split into that many parts."]);

            return ([], errors);
        }

        return (amounts, errors);
    }
}
