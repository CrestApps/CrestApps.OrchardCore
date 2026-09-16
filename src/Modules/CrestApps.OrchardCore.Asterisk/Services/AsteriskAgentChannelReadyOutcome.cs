namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Describes how a wait for a module-originated agent channel's readiness ended, so a caller can distinguish a
/// genuine no-answer from a cancellation caused by tenant shutdown. Collapsing the two would persist a canceled
/// connect attempt as an agent no-answer and corrupt interaction outcomes and agent metrics.
/// </summary>
internal enum AsteriskAgentChannelReadyOutcome
{
    /// <summary>
    /// The agent channel entered the Stasis application and is ready to be bridged.
    /// </summary>
    Ready,

    /// <summary>
    /// The wait ended without the channel becoming ready, either because the answer timeout elapsed or because a
    /// superseding attempt released the registration. This is a genuine no-answer for the current attempt.
    /// </summary>
    NotReady,

    /// <summary>
    /// The supplied cancellation token was canceled before the channel became ready. The attempt was abandoned by
    /// the host rather than declined by the agent, so it must not be recorded as a no-answer.
    /// </summary>
    Canceled,
}
