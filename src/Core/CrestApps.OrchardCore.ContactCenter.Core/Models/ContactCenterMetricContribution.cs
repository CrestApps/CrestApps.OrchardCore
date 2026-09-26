namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single unfolded contribution to a daily event count, read without loading the document that
/// holds it. A caller that has to account for every contribution waiting to be folded needs only the bucket it
/// counts toward, how much it counts, and a position it can resume a walk from.
/// </summary>
/// <param name="DocumentId">The document identifier, which is also the position a walk resumes from.</param>
/// <param name="DateKey">The day the contribution counts toward, formatted as <c>yyyy-MM-dd</c>.</param>
/// <param name="EventType">The domain event type being counted.</param>
/// <param name="Count">The number of events this contribution represents.</param>
public sealed record ContactCenterMetricContribution(
    long DocumentId,
    string DateKey,
    string EventType,
    long Count);
