namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Names the units of Contact Center work that can be quiesced and drained independently.
/// </summary>
/// <remarks>
/// A capability is what the framework keys feature-owned work on. It is deliberately not an Orchard
/// feature id: the framework has to run on a host that has no notion of features, and a host that
/// does have them decides for itself which of its deployment units map to which capability, through
/// <see cref="ContactCenterFeatureCapabilityMapping"/>.
/// <para>
/// The values are the Orchard feature ids with the host-specific prefix removed, so an Orchard
/// tenant that was quiescing correctly before still is.
/// </para>
/// </remarks>
public static class ContactCenterCapabilities
{
    /// <summary>
    /// Work owned by the base Contact Center capability.
    /// </summary>
    public const string Core = "ContactCenter";

    /// <summary>
    /// Work owned by the AgentServices capability.
    /// </summary>
    public const string AgentServices = "ContactCenter.AgentServices";

    /// <summary>
    /// Work owned by the Agents capability.
    /// </summary>
    public const string Agents = "ContactCenter.Agents";

    /// <summary>
    /// Work owned by the BusinessHours capability.
    /// </summary>
    public const string BusinessHours = "ContactCenter.BusinessHours";

    /// <summary>
    /// Work owned by the Queues capability.
    /// </summary>
    public const string Queues = "ContactCenter.Queues";

    /// <summary>
    /// Work owned by the Dialer capability.
    /// </summary>
    public const string Dialer = "ContactCenter.Dialer";

    /// <summary>
    /// Work owned by the Dialer.Paced capability.
    /// </summary>
    public const string DialerPaced = "ContactCenter.Dialer.Paced";

    /// <summary>
    /// Work owned by the ProviderInbox capability.
    /// </summary>
    public const string ProviderInbox = "ContactCenter.ProviderInbox";

    /// <summary>
    /// Work owned by the Voice capability.
    /// </summary>
    public const string Voice = "ContactCenter.Voice";

    /// <summary>
    /// Work owned by the Voice.Media capability.
    /// </summary>
    public const string VoiceMedia = "ContactCenter.Voice.Media";

    /// <summary>
    /// Work owned by the InboundVoice capability.
    /// </summary>
    public const string InboundVoice = "ContactCenter.InboundVoice";

    /// <summary>
    /// Work owned by the Recording.Core capability.
    /// </summary>
    public const string RecordingCore = "ContactCenter.Recording.Core";

    /// <summary>
    /// Work owned by the Recording capability.
    /// </summary>
    public const string Recording = "ContactCenter.Recording";

    /// <summary>
    /// Work owned by the SecureCapture capability.
    /// </summary>
    public const string SecureCapture = "ContactCenter.SecureCapture";

    /// <summary>
    /// Work owned by the Supervision capability.
    /// </summary>
    public const string Supervision = "ContactCenter.Supervision";

    /// <summary>
    /// Work owned by the RealTime capability.
    /// </summary>
    public const string RealTime = "ContactCenter.RealTime";

    /// <summary>
    /// Work owned by the AgentEntitlements capability.
    /// </summary>
    public const string AgentEntitlements = "ContactCenter.AgentEntitlements";
}
