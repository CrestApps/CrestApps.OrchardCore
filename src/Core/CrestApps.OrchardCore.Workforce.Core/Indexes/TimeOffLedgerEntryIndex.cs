using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of time-off ledger entries.
/// </summary>
public sealed class TimeOffLedgerEntryIndex : CatalogItemIndex
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
    /// Gets or sets the day the change counts from, as yyyyMMdd.
    /// </summary>
    public int EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the key that keeps the change from being written twice.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the entry was written.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
