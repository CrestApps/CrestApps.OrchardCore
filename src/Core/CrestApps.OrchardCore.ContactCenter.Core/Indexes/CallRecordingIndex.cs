using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// The YesSql index the call recordings page searches.
/// </summary>
public sealed class CallRecordingIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the kind of call that was recorded.
    /// </summary>
    public CallRecordingSource Source { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the recording.
    /// </summary>
    public string ProviderRecordingId { get; set; }

    /// <summary>
    /// Gets or sets the recorded Contact Center interaction, when the call is one.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent on the call.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number or address.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets who placed the call.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the length of the recording in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recording is in the media store and can be played.
    /// </summary>
    public bool IsStored { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recording was erased.
    /// </summary>
    public bool IsErased { get; set; }
}
