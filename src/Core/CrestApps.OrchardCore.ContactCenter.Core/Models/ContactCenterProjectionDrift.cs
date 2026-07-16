namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single detected discrepancy between the count a projection stores and the count recomputed
/// from the source-of-truth event log for one day and event type.
/// </summary>
public sealed class ContactCenterProjectionDrift
{
    /// <summary>
    /// Gets the day the discrepancy applies to, formatted as <c>yyyy-MM-dd</c>.
    /// </summary>
    public string DateKey { get; init; }

    /// <summary>
    /// Gets the domain event type the discrepancy applies to.
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets the count recomputed from the event log.
    /// </summary>
    public long ExpectedCount { get; init; }

    /// <summary>
    /// Gets the count currently stored in the projection.
    /// </summary>
    public long ActualCount { get; init; }
}
