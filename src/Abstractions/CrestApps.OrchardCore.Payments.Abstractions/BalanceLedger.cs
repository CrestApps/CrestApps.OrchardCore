namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Changes a stored money balance and records the change, the same way for every balance: gift cards, store credit and
/// the like. A balance never goes below zero and is kept at its currency's precision.
/// </summary>
public static class BalanceLedger
{
    /// <summary>
    /// Adds <paramref name="amount"/> (negative to spend) to a balance, never below zero and rounded to the currency's
    /// precision, and appends an entry with the change that was actually made.
    /// </summary>
    /// <typeparam name="TEntry">The balance's own entry type.</typeparam>
    /// <param name="balance">The balance before the change.</param>
    /// <param name="amount">The amount to add, or a negative amount to spend.</param>
    /// <param name="currency">The balance's currency.</param>
    /// <param name="activity">The balance's entries, which the new entry is appended to.</param>
    /// <param name="reason">Why the balance changes.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="sessionId">The checkout session the change is made in, if any.</param>
    /// <param name="actor">Who makes the change, if a person does.</param>
    /// <returns>The balance after the change.</returns>
    public static decimal ChangeBalance<TEntry>(
        decimal balance,
        decimal amount,
        string currency,
        IList<TEntry> activity,
        string reason,
        DateTime utcNow,
        string sessionId = null,
        string actor = null)
        where TEntry : BalanceLedgerEntry, new()
    {
        ArgumentNullException.ThrowIfNull(activity);

        var after = Money.Round(Math.Max(0m, balance + amount), currency);

        activity.Add(new TEntry
        {
            CreatedUtc = utcNow,
            Amount = after - balance,
            Balance = after,
            Reason = reason,
            SessionId = sessionId,
            Actor = actor,
        });

        return after;
    }
}
