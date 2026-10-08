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
    /// The key, in a takeover's <see cref="ContactCenterVoiceProviderResult.Metadata"/>, that says the leg the call was
    /// taken over on is an ordinary call of the supervisor's own (value <c>true</c>): the platform records it as theirs,
    /// so their soft phone lists it and mutes, holds and hangs it up like any other call.
    /// </summary>
    public const string TakeOverLegIsOwnCallMetadataKey = "takeOverLegIsOwnCall";

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
