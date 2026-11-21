using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Ledgers;

/// <summary>
/// One dated movement of a quantity in a ledger, such as hours of time off granted or education credits earned.
/// Entries are never changed: a correction is another entry.
/// </summary>
public sealed class QuantityLedgerEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets whose balance the entry moves, such as a worker or a user.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets which balance of the account it moves, such as a time-off policy or a credit type.
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    /// Gets or sets what kind of movement it is, in the owner's words, such as <c>Accrual</c> or <c>Earned</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the quantity: positive adds to the balance, negative takes from it.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day the entry counts from.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the last day the entry counts, or <see langword="null"/> when it never expires.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets what the entry came from, such as a request id or an enrollment id.
    /// </summary>
    public string SourceRef { get; set; }

    /// <summary>
    /// Gets or sets a key that is posted once: posting another entry with the same key returns the first one.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets who posted the entry.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets when the entry was posted.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the entry counts on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when the entry has started and not expired on that day.</returns>
    public bool CountsOn(DateOnly date) => EffectiveDate <= date && (ExpiresOn is null || ExpiresOn >= date);
}
