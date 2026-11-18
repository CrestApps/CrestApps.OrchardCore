using System.Globalization;

namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Writes an amount of money the way the site shows money to people: on pages, in documents such as invoices and
/// receipts, and in e-mails. Views and document builders ask this service rather than formatting a number themselves, so
/// a site that shows money another way replaces one service.
/// </summary>
public interface IMoneyFormatter
{
    /// <summary>
    /// Writes an amount in its currency.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The ISO-4217 code of the currency.</param>
    /// <param name="culture">The culture of the reader, or <see langword="null"/> for the current culture. A document
    /// written for a customer passes the customer's culture.</param>
    /// <returns>The amount, for example <c>$1,250.00</c>.</returns>
    string Format(decimal amount, string currency, CultureInfo culture = null);
}
