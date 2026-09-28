using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a reusable outbound dialing configuration: a dialing mode, provider, and compliance settings
/// that can be applied to any campaign. A profile is not tied to a campaign or queue; the campaign is chosen
/// when inventory is loaded (on the activity batch), and each loaded activity carries the profile that dials it.
/// </summary>
public sealed class DialerProfile : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the dialer profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the dialer profile.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the dialing mode that controls pacing and agent reservation behavior.
    /// </summary>
    public DialerMode Mode { get; set; } = DialerMode.Preview;

    /// <summary>
    /// Gets or sets the technical name of the Contact Center voice provider that places calls, or null for the default.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the number of calls placed per available agent for power dialing.
    /// </summary>
    public int CallsPerAgent { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of dialing attempts allowed per activity.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay, in minutes, before a no-answer activity is retried.
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the seconds added to a preview offer when the agent asks for more time.
    /// </summary>
    /// <remarks>
    /// Reviewing a record before dialing sometimes takes longer than the offer allows, and losing the offer
    /// means the review work is thrown away and the record goes back to the queue. Asking for more time keeps
    /// the record with the agent who has already started on it.
    /// </remarks>
    public int PreviewExtensionSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets how many times one preview offer may be extended.
    /// </summary>
    /// <remarks>
    /// A cap is what keeps "more time" from becoming "indefinitely", which would let one agent hold a record
    /// out of the queue for as long as they liked.
    /// </remarks>
    public int MaxPreviewExtensions { get; set; } = 2;

    /// <summary>
    /// Gets or sets the caller identifier presented to the customer when supported.
    /// </summary>
    public string CallerId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 region a destination is read in when it carries no country
    /// calling code. A campaign's activities are commonly imported in national format, and without a region
    /// such a destination cannot be canonicalized, so it cannot be compared with a do-not-call registry or
    /// matched to a regional calling calendar and the attempt is suppressed instead.
    /// </summary>
    public string DefaultRegionCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether do-not-call and communication preferences suppress activities.
    /// </summary>
    public bool RespectDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether calls are restricted by business-hours calendars.
    /// </summary>
    public bool EnforceCallingWindow { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether outbound dialing is gated by a rolling abandonment-rate cap.
    /// The cap only constrains automated pacing modes, because manual and preview dialing bind an agent to
    /// every call and cannot abandon a connected party.
    /// </summary>
    public bool EnforceAbandonmentCap { get; set; }

    /// <summary>
    /// Gets or sets the maximum tolerated rolling abandonment rate, expressed as a percentage of calls a
    /// live person answered. A value of 3 keeps abandonment at or below three percent.
    /// </summary>
    public double MaxAbandonmentRatePercent { get; set; } = 3;

    /// <summary>
    /// Gets or sets the minimum number of live-answered calls that must accumulate in the rolling window
    /// before the abandonment rate is enforced, avoiding volatile suppression on small samples.
    /// </summary>
    public int AbandonmentSampleFloor { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether an abandoned automated call plays a safe-harbor announcement
    /// that identifies the caller instead of being dropped silently.
    /// </summary>
    public bool SafeHarborEnabled { get; set; }

    /// <summary>
    /// Gets or sets the safe-harbor announcement played to a live party when no agent connects in time.
    /// </summary>
    public string SafeHarborMessage { get; set; }

    /// <summary>
    /// Gets or sets the default business-hours calendar used to evaluate outbound calls.
    /// </summary>
    public string CallingCalendarId { get; set; }

    /// <summary>
    /// Gets or sets region-specific business-hours calendar overrides keyed by ISO 3166-1 alpha-2 region code.
    /// </summary>
    public IDictionary<string, string> RegionalCallingCalendarIds { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether the dialer profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the dialer profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the dialer profile was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
