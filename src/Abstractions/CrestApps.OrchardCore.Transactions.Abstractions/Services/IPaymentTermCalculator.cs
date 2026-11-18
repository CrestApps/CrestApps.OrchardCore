using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Turns a <see cref="PaymentTerm"/> into dates and amounts. The dates are local dates in the site's time zone, so a
/// term never moves a day because of a time zone; convert a local due date to a UTC time only when storing it on the
/// ledger.
/// </summary>
public interface IPaymentTermCalculator
{
    /// <summary>
    /// Returns the date a debt is due under a term.
    /// </summary>
    /// <param name="term">The term, or <see langword="null"/> for a debt due on the issue date.</param>
    /// <param name="issueDate">The local date the document was issued.</param>
    /// <returns>The local due date, never before the issue date.</returns>
    DateOnly GetDueDate(PaymentTerm term, DateOnly issueDate);

    /// <summary>
    /// Returns the discount a term offers for paying early, or <see langword="null"/> when it offers none.
    /// </summary>
    /// <param name="term">The term.</param>
    /// <param name="issueDate">The local date the document was issued.</param>
    /// <param name="amount">The amount the discount is a percent of.</param>
    /// <param name="currency">The ISO-4217 currency the amount is in, which sets the rounding.</param>
    /// <returns>The discount and the last date it is offered.</returns>
    EarlyPaymentDiscount? GetEarlyPaymentDiscount(PaymentTerm term, DateOnly issueDate, decimal amount, string currency);
}
