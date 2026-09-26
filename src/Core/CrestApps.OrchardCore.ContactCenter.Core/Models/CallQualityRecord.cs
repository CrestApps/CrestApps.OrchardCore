using CrestApps.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The quality of one call leg, measured once the leg was over, and tied to the interaction and agent it belongs to.
/// </summary>
/// <remarks>
/// A leg can be measured twice: by the agent's soft phone, which hears what the agent hears, and by the provider,
/// which measures every leg it carries, including the customer's. Each measurement is its own record, told apart by
/// <see cref="Source"/>, so a poor call can be traced to the side it was poor on. The headline figures are copied out
/// of the raw measurement so reports can compare the two sources without knowing either shape.
/// </remarks>
public sealed class CallQualityRecord : CatalogItem
{
    /// <summary>
    /// Gets or sets the key that makes a measurement recorded once however often it is delivered: the source and the
    /// provider call-control id of the leg.
    /// </summary>
    public string RecordKey { get; set; }

    /// <summary>
    /// Gets or sets where the measurement came from.
    /// </summary>
    public CallQualitySource Source { get; set; }

    /// <summary>
    /// Gets or sets how the leg rated.
    /// </summary>
    public CallQualityRating Rating { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider carrying the leg.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's call-control id for the leg.
    /// </summary>
    public string ProviderCallControlId { get; set; }

    /// <summary>
    /// Gets or sets the provider's leg id.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the provider's session id, shared by every leg of one call.
    /// </summary>
    public string ProviderSessionId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the leg belongs to, or <see langword="null"/> for a call the contact center did not
    /// route, such as an extension call.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the call session the leg belongs to.
    /// </summary>
    public string CallSessionId { get; set; }

    /// <summary>
    /// Gets or sets the part the leg plays in the call.
    /// </summary>
    public CallPartyRole LegRole { get; set; }

    /// <summary>
    /// Gets or sets the agent on the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the user the agent signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the queue the call came through.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the mean opinion score the leg is rated on, or <see langword="null"/> when none was measured.
    /// </summary>
    public double? Mos { get; set; }

    /// <summary>
    /// Gets or sets the share of the audio that was lost, as a percentage, or <see langword="null"/> when not measured.
    /// </summary>
    public double? LossPercent { get; set; }

    /// <summary>
    /// Gets or sets the jitter, in milliseconds, or <see langword="null"/> when not measured.
    /// </summary>
    public double? JitterMs { get; set; }

    /// <summary>
    /// Gets or sets the round-trip time, in milliseconds, or <see langword="null"/> when not measured.
    /// </summary>
    public double? RoundTripMs { get; set; }

    /// <summary>
    /// Gets or sets how long the leg was measured for, in seconds, or <see langword="null"/> when unknown.
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the soft phone's end-of-call summary, for a browser measurement.
    /// </summary>
    public CallQualityReport Browser { get; set; }

    /// <summary>
    /// Gets or sets the provider's statistics, for a provider measurement.
    /// </summary>
    public ProviderCallQualityStats Provider { get; set; }

    /// <summary>
    /// Gets or sets when the leg was measured: the end of the leg.
    /// </summary>
    public DateTime ObservedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the record was written.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Builds the key that identifies one source's measurement of one leg.
    /// </summary>
    /// <param name="source">Where the measurement came from.</param>
    /// <param name="providerCallControlId">The provider's call-control id for the leg.</param>
    /// <returns>The key.</returns>
    public static string BuildRecordKey(CallQualitySource source, string providerCallControlId)
        => $"{source}|{providerCallControlId}";
}
