namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Describes the scope of a Contact Center preview reset.
/// </summary>
public enum ContactCenterPreviewResetScope
{
    /// <summary>
    /// Deletes operational traffic data — interactions, events, calls, queue items, sessions, outbox and inbox
    /// messages, metrics, and projection state — while preserving operator-authored configuration.
    /// </summary>
    OperationalData,

    /// <summary>
    /// Deletes every Contact Center data set, including operator-authored configuration.
    /// </summary>
    All,
}
