namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Projects between the canonical twelve-state voice call vocabulary and the seven-state
/// telephony soft-phone vocabulary, and derives the terminal voice call state implied by a
/// provider-reported hangup cause.
/// <para>
/// This type is the only place either projection may be written. The soft-phone vocabulary is a
/// strict, lossy projection of the canonical vocabulary: four distinct terminal outcomes
/// collapse onto <see cref="CallState.Disconnected"/> or <see cref="CallState.Failed"/>. When each
/// call site was free to write its own projection, those collapses were applied inconsistently and
/// in the widening direction they discarded the outcome entirely, so every provider hangup became
/// <see cref="VoiceCallState.Ended"/> regardless of why the call ended.
/// </para>
/// </summary>
public static class VoiceCallStateProjection
{
    /// <summary>
    /// Widens a soft-phone call state into the canonical voice call state, refining the
    /// terminal outcome from the provider-reported hangup cause when one is available.
    /// </summary>
    /// <param name="state">The soft-phone call state reported by the provider.</param>
    /// <param name="isOnHold">Whether the provider additionally reports the call as held.</param>
    /// <param name="hangupCause">The provider-reported hangup cause, when the call has ended.</param>
    /// <returns>The canonical voice call state.</returns>
    /// <remarks>
    /// <see cref="CallState.Idle"/> is the soft phone's "no live call" sentinel rather than a call
    /// state, so widening it for a call a provider still reports means the call is over. It therefore
    /// maps to <see cref="VoiceCallState.Ended"/> and does not round-trip back from
    /// <see cref="ToTelephonyCallState"/>.
    /// </remarks>
    public static VoiceCallState ToVoiceCallState(
        CallState state,
        bool isOnHold = false,
        HangupCause? hangupCause = null)
    {
        return state switch
        {
            CallState.Idle => VoiceCallState.Ended,
            CallState.Connecting => VoiceCallState.Dialing,
            CallState.Ringing => VoiceCallState.Ringing,
            CallState.Connected when isOnHold => VoiceCallState.OnHold,
            CallState.Connected => VoiceCallState.Connected,
            CallState.OnHold => VoiceCallState.OnHold,
            CallState.Disconnected => ToTerminalVoiceCallState(hangupCause, VoiceCallState.Ended),
            CallState.Failed => ToTerminalVoiceCallState(hangupCause, VoiceCallState.Failed),
            _ => VoiceCallState.Ended,
        };
    }

    /// <summary>
    /// Narrows the canonical Contact Center call state into the soft-phone call state.
    /// </summary>
    /// <param name="state">The canonical voice call state.</param>
    /// <returns>The soft-phone call state.</returns>
    public static CallState ToTelephonyCallState(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Planned => CallState.Idle,
            VoiceCallState.Dialing => CallState.Connecting,
            VoiceCallState.Ringing => CallState.Ringing,
            VoiceCallState.Connected => CallState.Connected,
            VoiceCallState.OnHold => CallState.OnHold,
            VoiceCallState.Ending => CallState.Disconnected,
            VoiceCallState.Ended => CallState.Disconnected,
            VoiceCallState.Transferred => CallState.Disconnected,
            VoiceCallState.Canceled => CallState.Disconnected,
            VoiceCallState.NoAnswer => CallState.Failed,
            VoiceCallState.Rejected => CallState.Failed,
            VoiceCallState.Failed => CallState.Failed,
            _ => CallState.Idle,
        };
    }

    /// <summary>
    /// Resolves the terminal Contact Center call state implied by a provider-reported hangup cause.
    /// </summary>
    /// <param name="hangupCause">The provider-reported hangup cause, when one was reported.</param>
    /// <param name="fallback">The terminal state to use when no usable cause was reported.</param>
    /// <returns>The terminal Contact Center call state.</returns>
    public static VoiceCallState ToTerminalVoiceCallState(
        HangupCause? hangupCause,
        VoiceCallState fallback)
    {
        return hangupCause switch
        {
            HangupCause.NormalClearing => VoiceCallState.Ended,
            HangupCause.AnsweringMachine => VoiceCallState.Ended,
            HangupCause.Busy => VoiceCallState.Rejected,
            HangupCause.Rejected => VoiceCallState.Rejected,
            HangupCause.NoAnswer => VoiceCallState.NoAnswer,
            HangupCause.Canceled => VoiceCallState.Canceled,
            HangupCause.Congestion => VoiceCallState.Failed,
            HangupCause.Failed => VoiceCallState.Failed,
            _ => fallback,
        };
    }

    /// <summary>
    /// Determines whether the supplied Contact Center call state is terminal, meaning the call can
    /// no longer change state.
    /// </summary>
    /// <param name="state">The Contact Center call state to evaluate.</param>
    /// <returns><see langword="true"/> when the state is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(VoiceCallState state)
    {
        return state is VoiceCallState.Ended or
            VoiceCallState.Failed or
            VoiceCallState.NoAnswer or
            VoiceCallState.Rejected or
            VoiceCallState.Canceled or
            VoiceCallState.Transferred;
    }
}
