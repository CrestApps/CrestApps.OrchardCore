namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Where a call-quality measurement came from.
/// </summary>
public enum CallQualitySource
{
    /// <summary>
    /// The agent's soft phone, measuring the leg it holds.
    /// </summary>
    Browser,

    /// <summary>
    /// The telephony provider, measuring a leg it carries.
    /// </summary>
    Provider,
}

/// <summary>
/// One leg's call quality, measured once the leg is over, and handed to whatever keeps a record of it.
/// </summary>
/// <remarks>
/// Telephony knows the leg only by the provider's ids, and nothing about the interaction, queue or agent it belongs
/// to; an observer that does know those ties the measurement to them. The provider call-control id is the one both
/// sources share, and the one a contact-center call knows its legs by.
/// </remarks>
public sealed class CallQualityObservation
{
    /// <summary>
    /// Gets or sets where the measurement came from.
    /// </summary>
    public CallQualitySource Source { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider carrying the leg.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the user whose soft phone measured the leg, for a browser measurement.
    /// </summary>
    public string UserId { get; set; }

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
    /// Gets or sets how the leg rated.
    /// </summary>
    public CallQualityRating Rating { get; set; }

    /// <summary>
    /// Gets or sets when the measurement was taken: the end of the leg.
    /// </summary>
    public DateTime ObservedUtc { get; set; }

    /// <summary>
    /// Gets or sets the soft phone's end-of-call summary, for a browser measurement.
    /// </summary>
    public CallQualityReport Browser { get; set; }

    /// <summary>
    /// Gets or sets the provider's statistics, for a provider measurement.
    /// </summary>
    public ProviderCallQualityStats Provider { get; set; }
}
