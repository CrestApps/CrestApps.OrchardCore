namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The discount a payment term offers for paying early: how much is taken off, and the last local date it is offered.
/// </summary>
/// <param name="Deadline">The last local date the discount is offered.</param>
/// <param name="Amount">The amount taken off, rounded to the currency.</param>
public readonly record struct EarlyPaymentDiscount(DateOnly Deadline, decimal Amount)
{
    /// <summary>
    /// Returns whether a payment made on a local date gets the discount.
    /// </summary>
    /// <param name="paidOn">The local date of the payment.</param>
    /// <returns><see langword="true"/> on or before the deadline.</returns>
    public bool AppliesOn(DateOnly paidOn)
        => paidOn <= Deadline;
}
