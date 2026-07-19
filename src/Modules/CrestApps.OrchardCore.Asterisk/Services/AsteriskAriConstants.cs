namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Contains Asterisk ARI constants that are specific to Contact Center orchestration.
/// </summary>
internal static class AsteriskAriConstants
{
    /// <summary>
    /// The bridge type used to mix caller and agent media.
    /// </summary>
    public const string MixingBridgeType = "mixing";

    /// <summary>
    /// The bridge type used to hold an inbound caller (with music on hold) while an offer is routed.
    /// </summary>
    public const string HoldingBridgeType = "holding";

    /// <summary>
    /// The deterministic prefix used for Contact Center caller-to-agent bridges.
    /// </summary>
    public const string AgentBridgePrefix = "crestapps-bridge-";

    /// <summary>
    /// The deterministic prefix used for originated Contact Center agent legs.
    /// </summary>
    public const string AgentChannelPrefix = "crestapps-agent-";

    /// <summary>
    /// The value stamped on channels originated by this module.
    /// </summary>
    public const string OriginationMarkerValue = "true";

    /// <summary>
    /// The call-session metadata key containing the active originated agent channel id.
    /// </summary>
    public const string AgentChannelMetadataKey = "asteriskAgentChannelId";

    /// <summary>
    /// The Asterisk channel technology used to reach a browser softphone provisioned through PJSIP realtime.
    /// </summary>
    public const string PjsipEndpointTechnology = "PJSIP";

    /// <summary>
    /// The maximum time, in seconds, to wait for an originated agent channel to enter the Stasis application
    /// (that is, for the agent to answer) before a caller-to-agent connect attempt is treated as unanswered.
    /// </summary>
    public const int AgentAnswerTimeoutSeconds = 30;

    /// <summary>
    /// The deterministic prefix used for Contact Center conversation recordings. Combined with the owning
    /// interaction identifier it yields a stable recording name so pause, resume, and stop address the same live
    /// recording without additional state.
    /// </summary>
    public const string RecordingNamePrefix = "crestapps-recording-";

    /// <summary>
    /// The media format used when starting a Contact Center bridge recording.
    /// </summary>
    public const string RecordingFormat = "wav";

    /// <summary>
    /// The <c>ifExists</c> value used when starting a bridge recording so a re-issued start reuses the same name.
    /// </summary>
    public const string RecordingIfExistsOverwrite = "overwrite";

    /// <summary>
    /// The <c>terminateOn</c> value used when starting a bridge recording so no DTMF key stops it.
    /// </summary>
    public const string RecordingTerminateOnNone = "none";

    /// <summary>
    /// The live-recording state Asterisk reports for a paused recording.
    /// </summary>
    public const string RecordingPausedState = "paused";

    /// <summary>
    /// The ARI relative path prefix through which a stored recording is retrieved by name.
    /// </summary>
    public const string StoredRecordingRetrievalPathPrefix = "recordings/stored/";

    /// <summary>
    /// The deterministic prefix used for the dedicated supervisor mixing bridge that joins the snoop leg to the
    /// originated supervisor endpoint for a listen-only or whisper engagement. Combined with the interaction and
    /// supervisor identity it yields a stable bridge id so a later stop addresses the same bridge.
    /// </summary>
    public const string SupervisorBridgePrefix = "crestapps-super-bridge-";

    /// <summary>
    /// The deterministic prefix used for the originated supervisor endpoint leg of a monitoring engagement.
    /// Combined with the interaction and supervisor identity it yields a stable channel id so retries are
    /// idempotent and a later stop can hang up the same leg.
    /// </summary>
    public const string SupervisorChannelPrefix = "crestapps-super-";

    /// <summary>
    /// The deterministic prefix used for the snoop channel that carries the conversation audio to the supervisor
    /// in a listen-only or whisper engagement. Combined with the interaction and supervisor identity it yields a
    /// stable snoop id so a later stop can hang up the same snoop leg.
    /// </summary>
    public const string SupervisorSnoopPrefix = "crestapps-snoop-";

    /// <summary>
    /// The snoop <c>spy</c> direction that lets the supervisor hear audio flowing both toward and away from the
    /// spied channel (both parties of the conversation).
    /// </summary>
    public const string SnoopSpyBoth = "both";

    /// <summary>
    /// The snoop <c>whisper</c> direction that injects no supervisor audio into the spied channel, keeping a
    /// listen-only (monitor) engagement silent.
    /// </summary>
    public const string SnoopWhisperNone = "none";

    /// <summary>
    /// The snoop <c>whisper</c> direction that injects the supervisor audio outward into the spied channel only,
    /// so a whisper engagement is heard by the agent but never by the customer.
    /// </summary>
    public const string SnoopWhisperOut = "out";

    /// <summary>
    /// The maximum time, in seconds, to wait for an originated supervisor channel to enter the Stasis application
    /// (that is, for the supervisor's softphone to answer) before a monitoring engagement is treated as unanswered.
    /// </summary>
    public const int SupervisorAnswerTimeoutSeconds = 30;
}
