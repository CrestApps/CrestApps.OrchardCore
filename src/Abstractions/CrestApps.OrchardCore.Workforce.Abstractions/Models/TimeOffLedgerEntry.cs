using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One change to a worker's balance under a policy. The ledger is only ever added to; a balance is the sum of its
/// entries up to a day.
/// </summary>
public sealed class TimeOffLedgerEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets why the balance changed.
    /// </summary>
    public TimeOffLedgerKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the change, in the type's unit; negative when the balance goes down.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day the change counts from.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets what caused the change, such as a request id or an accrual period.
    /// </summary>
    public string SourceRef { get; set; }

    /// <summary>
    /// Gets or sets the key that keeps the same change from being written twice; unique.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the entry was written.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
