namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// How a phone call that is no Contact Center interaction is put together, as a monitoring provider reads it: what a
/// supervisor engagement on it is started, changed and stopped with.
/// </summary>
public sealed class ContactCenterPhoneCallMonitoringTarget
{
    /// <summary>
    /// The key, in <see cref="ContactCenterVoiceMonitoringRequest.Metadata"/>, of the conference the supervisor joins.
    /// </summary>
    public const string ConferenceMetadataKey = "conferenceName";

    /// <summary>
    /// Gets or sets the call as the provider's monitoring requests name it (<see cref="ContactCenterVoiceMonitoringRequest.ProviderCallId"/>).
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the leg of the agent being monitored: the one a coaching supervisor is heard by.
    /// </summary>
    public string AgentLegId { get; set; }

    /// <summary>
    /// Gets or sets the leg of the other party: the one left with the supervisor after a takeover.
    /// </summary>
    public string OtherPartyLegId { get; set; }

    /// <summary>
    /// Gets or sets the conference the supervisor joins.
    /// </summary>
    public string ConferenceName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supervisor can take the call over from the agent.
    /// </summary>
    public bool CanTakeOver { get; set; }
}
