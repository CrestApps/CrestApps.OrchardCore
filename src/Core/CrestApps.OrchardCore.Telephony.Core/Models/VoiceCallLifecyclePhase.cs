namespace CrestApps.OrchardCore.Telephony.Core.Models;

/// <summary>
/// Describes how far a call has progressed through its lifecycle, independently of any consumer's own
/// call-state vocabulary. Ordering a provider stream only requires knowing whether a delivery moves the
/// call forward, so the ingress layer reasons in phases and each consumer maps its own states onto them.
/// The declared order is meaningful: a delivery whose phase is lower than the phase already recorded for
/// the stream is a regression and is never applied.
/// </summary>
public enum VoiceCallLifecyclePhase
{
    /// <summary>
    /// The call exists as intent only and has not been offered to the network.
    /// </summary>
    Planned = 0,

    /// <summary>
    /// The call is being placed or is alerting a destination, but no party has answered.
    /// </summary>
    Alerting = 1,

    /// <summary>
    /// The call is answered and media is established, including while it is held.
    /// </summary>
    Established = 2,

    /// <summary>
    /// The call is being torn down but has not reached a final outcome.
    /// </summary>
    Ending = 3,

    /// <summary>
    /// The call has reached a final outcome and no further delivery can advance it.
    /// </summary>
    Terminal = 4,
}
