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
}
