using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Projects between the canonical twelve-state Contact Center call vocabulary and the seven-state
/// telephony soft-phone vocabulary, and derives the terminal Contact Center state implied by a
/// provider-reported hangup cause.
/// <para>
/// This type is the only place either projection may be written. The soft-phone vocabulary is a
/// strict, lossy projection of the Contact Center vocabulary: four distinct terminal outcomes
/// collapse onto <see cref="CallState.Disconnected"/> or <see cref="CallState.Failed"/>. When each
/// call site was free to write its own projection, those collapses were applied inconsistently and
/// in the widening direction they discarded the outcome entirely, so every provider hangup became
/// <see cref="ContactCenterCallState.Ended"/> regardless of why the call ended.
/// </para>
/// </summary>
public static class ContactCenterCallStateProjection
{
    /// <summary>
    /// Widens a soft-phone call state into the canonical Contact Center call state, refining the
    /// terminal outcome from the provider-reported hangup cause when one is available.
    /// </summary>
    /// <param name="state">The soft-phone call state reported by the provider.</param>
    /// <param name="isOnHold">Whether the provider additionally reports the call as held.</param>
    /// <param name="hangupCause">The provider-reported hangup cause, when the call has ended.</param>
    /// <returns>The canonical Contact Center call state.</returns>
    /// <remarks>
    /// <see cref="CallState.Idle"/> is the soft phone's "no live call" sentinel rather than a call
    /// state, so widening it for a call a provider still reports means the call is over. It therefore
    /// maps to <see cref="ContactCenterCallState.Ended"/> and does not round-trip back from
    /// <see cref="ToTelephonyCallState"/>.
    /// </remarks>
    public static ContactCenterCallState ToContactCenterCallState(
        CallState state,
        bool isOnHold = false,
        HangupCause? hangupCause = null)
    {
        return state switch
        {
            CallState.Idle => ContactCenterCallState.Ended,
            CallState.Connecting => ContactCenterCallState.Dialing,
            CallState.Ringing => ContactCenterCallState.Ringing,
            CallState.Connected when isOnHold => ContactCenterCallState.OnHold,
            CallState.Connected => ContactCenterCallState.Connected,
            CallState.OnHold => ContactCenterCallState.OnHold,
            CallState.Disconnected => ToTerminalContactCenterCallState(hangupCause, ContactCenterCallState.Ended),
            CallState.Failed => ToTerminalContactCenterCallState(hangupCause, ContactCenterCallState.Failed),
            _ => ContactCenterCallState.Ended,
        };
    }

    /// <summary>
    /// Narrows the canonical Contact Center call state into the soft-phone call state.
    /// </summary>
    /// <param name="state">The canonical Contact Center call state.</param>
    /// <returns>The soft-phone call state.</returns>
    public static CallState ToTelephonyCallState(ContactCenterCallState state)
    {
        return state switch
        {
            ContactCenterCallState.Planned => CallState.Idle,
            ContactCenterCallState.Dialing => CallState.Connecting,
            ContactCenterCallState.Ringing => CallState.Ringing,
            ContactCenterCallState.Connected => CallState.Connected,
            ContactCenterCallState.OnHold => CallState.OnHold,
            ContactCenterCallState.Ending => CallState.Disconnected,
            ContactCenterCallState.Ended => CallState.Disconnected,
            ContactCenterCallState.Transferred => CallState.Disconnected,
            ContactCenterCallState.Canceled => CallState.Disconnected,
            ContactCenterCallState.NoAnswer => CallState.Failed,
            ContactCenterCallState.Rejected => CallState.Failed,
            ContactCenterCallState.Failed => CallState.Failed,
            _ => CallState.Idle,
        };
    }

    /// <summary>
    /// Resolves the terminal Contact Center call state implied by a provider-reported hangup cause.
    /// </summary>
    /// <param name="hangupCause">The provider-reported hangup cause, when one was reported.</param>
    /// <param name="fallback">The terminal state to use when no usable cause was reported.</param>
    /// <returns>The terminal Contact Center call state.</returns>
    public static ContactCenterCallState ToTerminalContactCenterCallState(
        HangupCause? hangupCause,
        ContactCenterCallState fallback)
    {
        return hangupCause switch
        {
            HangupCause.NormalClearing => ContactCenterCallState.Ended,
            HangupCause.AnsweringMachine => ContactCenterCallState.Ended,
            HangupCause.Busy => ContactCenterCallState.Rejected,
            HangupCause.Rejected => ContactCenterCallState.Rejected,
            HangupCause.NoAnswer => ContactCenterCallState.NoAnswer,
            HangupCause.Canceled => ContactCenterCallState.Canceled,
            HangupCause.Congestion => ContactCenterCallState.Failed,
            HangupCause.Failed => ContactCenterCallState.Failed,
            _ => fallback,
        };
    }

    /// <summary>
    /// Determines whether the supplied Contact Center call state is terminal, meaning the call can
    /// no longer change state.
    /// </summary>
    /// <param name="state">The Contact Center call state to evaluate.</param>
    /// <returns><see langword="true"/> when the state is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(ContactCenterCallState state)
    {
        return state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred;
    }
}
