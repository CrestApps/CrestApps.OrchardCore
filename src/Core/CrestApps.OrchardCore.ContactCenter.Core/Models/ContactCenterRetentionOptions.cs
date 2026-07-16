namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Configures Contact Center data-governance retention windows. A value of zero for
/// <see cref="InteractionEventRetentionDays"/> disables purging so data is kept indefinitely. The floor
/// settings can only make retention more conservative (keep data longer); they never purge earlier than the
/// configured window.
/// </summary>
public sealed class ContactCenterRetentionOptions
{
    /// <summary>
    /// Gets or sets the number of days to retain durable interaction events before they are purged. A value of
    /// zero disables purging entirely so events are kept indefinitely.
    /// </summary>
    public int InteractionEventRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of days the durable event log must remain rebuildable. Because purging
    /// the event log destroys the ability to replay projections for the purged period, retention never purges
    /// events younger than this horizon even when <see cref="InteractionEventRetentionDays"/> is shorter. This
    /// guarantees projections can be rebuilt for at least this window. Zero applies no replay-horizon floor.
    /// </summary>
    public int ProjectionReplayHorizonDays { get; set; }

    /// <summary>
    /// Gets or sets a legal-hold floor, in days, below which interaction events are never purged regardless of
    /// the configured retention window. Raise it to satisfy a legal hold or regulatory minimum-retention
    /// obligation. Zero applies no legal-hold floor.
    /// </summary>
    public int LegalHoldMinimumDays { get; set; }
}
