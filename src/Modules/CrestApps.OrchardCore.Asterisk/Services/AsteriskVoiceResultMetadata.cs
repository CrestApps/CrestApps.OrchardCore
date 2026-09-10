namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Contains the Asterisk-private result-metadata keys the provider attaches to Contact Center voice results.
/// </summary>
/// <remarks>
/// These keys name ARI channels and bridges, which are Asterisk implementation details. No provider-neutral
/// consumer reads them, so they are owned by this module rather than declared in the shared Contact Center
/// contracts, where every other provider would inherit vocabulary it cannot honor.
/// </remarks>
internal static class AsteriskVoiceResultMetadata
{
    /// <summary>
    /// Identifies the originated supervisor endpoint channel that carries the supervisor's audio.
    /// </summary>
    public const string SupervisorChannelId = "supervisorChannelId";

    /// <summary>
    /// Identifies the snoop channel that carries the conversation audio to the supervisor in a listen-only or
    /// whisper engagement. It is omitted for a barge engagement, which uses no snoop.
    /// </summary>
    public const string SnoopChannelId = "snoopChannelId";

    /// <summary>
    /// Identifies the bridge the supervisor was joined to: a dedicated supervisor bridge for a listen-only or
    /// whisper engagement, or the main conversation bridge for a barge engagement.
    /// </summary>
    public const string SupervisorBridgeId = "supervisorBridgeId";

    /// <summary>
    /// Identifies the monitoring engagement mode that was executed.
    /// </summary>
    public const string MonitoringMode = "mode";

    /// <summary>
    /// Identifies the ARI channel of the new leg established for a completed transfer.
    /// </summary>
    public const string TransferNewChannelId = "transferNewChannelId";

    /// <summary>
    /// Identifies the ARI bridge the transfer was executed on.
    /// </summary>
    public const string TransferBridgeId = "transferBridgeId";

    /// <summary>
    /// Identifies the ARI channel of the participant leg established for a completed conference add.
    /// </summary>
    public const string ConferenceParticipantChannelId = "conferenceParticipantChannelId";

    /// <summary>
    /// Identifies the ARI bridge the conference participant was added to.
    /// </summary>
    public const string ConferenceBridgeId = "conferenceBridgeId";

    /// <summary>
    /// Identifies the ARI channel of the consult leg established for the destination agent.
    /// </summary>
    public const string AttendedTransferConsultChannelId = "attendedTransferConsultChannelId";

    /// <summary>
    /// Identifies the ARI bridge the attended transfer was executed on.
    /// </summary>
    public const string AttendedTransferBridgeId = "attendedTransferBridgeId";
}
