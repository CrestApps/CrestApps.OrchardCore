namespace CrestApps.OrchardCore.Telephony.Core.Models;

/// <summary>
/// Describes one normalized provider delivery as the ingress ordering rules see it. It carries only what
/// ordering needs, so the same rules apply to every provider and every consumer projection.
/// </summary>
public sealed class VoiceStreamDelivery
{
    /// <summary>
    /// Gets or sets the lifecycle phase the delivery reports.
    /// </summary>
    public VoiceCallLifecyclePhase Phase { get; set; }

    /// <summary>
    /// Gets or sets the provider sequence number stamped on the delivery, when the provider stamps one.
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the time the delivery occurred, in UTC.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}
