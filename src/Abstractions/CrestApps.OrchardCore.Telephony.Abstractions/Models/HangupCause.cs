namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Identifies the provider-neutral reason a voice call ended. Providers report their own release
/// causes — Q.850 cause codes, textual release reasons, or vendor-specific tokens — which are
/// normalized into these values so outbound compliance reporting, abandon analytics, and retry
/// policy can reason about how a call ended independently of the provider that ended it.
/// </summary>
public enum HangupCause
{
    /// <summary>
    /// The provider ended the call without reporting any release cause. This value exists so an
    /// unreported cause is recorded honestly rather than being silently reported as a normal
    /// clearing; it must never be produced when the provider did report a cause.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The call was answered and then released normally by one of the parties.
    /// </summary>
    NormalClearing = 1,

    /// <summary>
    /// The remote party was busy.
    /// </summary>
    Busy = 2,

    /// <summary>
    /// The call alerted the remote party but was never answered.
    /// </summary>
    NoAnswer = 3,

    /// <summary>
    /// The remote party or the network explicitly rejected the call.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// The call could not be completed because the network or a switch was congested, or no
    /// circuit was available. Unlike <see cref="Failed"/>, a congested call is normally retryable.
    /// </summary>
    Congestion = 5,

    /// <summary>
    /// The call failed for a reason that is not expected to succeed on retry, such as an
    /// unallocated number, an invalid number format, or an incompatible destination.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// The originating side abandoned the call before it was answered.
    /// </summary>
    Canceled = 7,

    /// <summary>
    /// The call was answered by an answering machine, voicemail greeting, or fax tone rather than
    /// by a live person.
    /// </summary>
    AnsweringMachine = 8,
}
