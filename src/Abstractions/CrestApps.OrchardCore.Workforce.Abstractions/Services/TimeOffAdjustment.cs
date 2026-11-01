using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A manual change to a worker's balance: an opening balance, a correction or a payout.
/// </summary>
public sealed class TimeOffAdjustment
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
    /// Gets or sets the kind: <see cref="TimeOffLedgerKind.Opening"/>, <see cref="TimeOffLedgerKind.Adjustment"/> or
    /// <see cref="TimeOffLedgerKind.Payout"/>.
    /// </summary>
    public TimeOffLedgerKind Kind { get; set; } = TimeOffLedgerKind.Adjustment;

    /// <summary>
    /// Gets or sets the change; negative to take away. A payout is always taken away.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the day it counts from.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets a key that keeps the same change from being made twice, such as an import row id.
    /// </summary>
    public string IdempotencyKey { get; set; }
}
