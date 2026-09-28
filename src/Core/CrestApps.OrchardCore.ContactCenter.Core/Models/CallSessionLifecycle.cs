using System.Collections.Frozen;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Declares which normalized call-state changes the domain admits for a <see cref="CallSession"/>.
/// <para>
/// Provider streams are ordered by lifecycle phase, which groups several states together — dialing and ringing
/// are both alerting, connected and held are both established — so phase ordering cannot tell a legal edge from
/// an impossible one within a phase. A call that goes from planned to held has not regressed and would be
/// applied, and the durations computed from it would be reported as though the call had been answered.
/// </para>
/// </summary>
public static class CallSessionLifecycle
{
    private static readonly FrozenDictionary<VoiceCallState, FrozenSet<VoiceCallState>> _transitions =
        new Dictionary<VoiceCallState, FrozenSet<VoiceCallState>>
        {
            // Ended is admitted from every alerting state. Providers do not all distinguish why a call that was
            // never answered stopped: some report NoAnswer or Rejected, others report a plain hangup. Refusing
            // the plain hangup would leave the session alerting forever and the offer would never be released,
            // which is a worse outcome than recording a less specific ending than the one that occurred.
            // Connected is admitted from planned because a session is not always created before the call is up.
            // On agent-device-native delivery the device answers and the provider reports the established call
            // in the same round trip that creates the record, so the session is born connected. Held is still
            // refused from planned: a call that was never answered cannot have been placed on hold, and that is
            // the reading this table exists to reject.
            [VoiceCallState.Planned] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Dialing,
                VoiceCallState.Ringing,
                VoiceCallState.Connected,
                VoiceCallState.Ended,
                VoiceCallState.Canceled,
                VoiceCallState.Failed,
            ]),
            [VoiceCallState.Dialing] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Ringing,
                VoiceCallState.Connected,
                VoiceCallState.Ending,
                VoiceCallState.Ended,
                VoiceCallState.NoAnswer,
                VoiceCallState.Rejected,
                VoiceCallState.Canceled,
                VoiceCallState.Failed,
            ]),
            [VoiceCallState.Ringing] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Connected,
                VoiceCallState.Ending,
                VoiceCallState.Ended,
                VoiceCallState.NoAnswer,
                VoiceCallState.Rejected,
                VoiceCallState.Canceled,
                VoiceCallState.Failed,
            ]),
            // Ringing is admitted back from the established states because the customer leg and the agent leg
            // share one session. A queue transfer keeps the customer up while the agent leg alerts the next
            // agent, so the session alerts again without the call ever having dropped. No terminal state admits
            // it, so this cannot resurrect a call that ended.
            [VoiceCallState.Connected] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Ringing,
                VoiceCallState.OnHold,
                VoiceCallState.Ending,
                VoiceCallState.Ended,
                VoiceCallState.Transferred,
                VoiceCallState.Failed,
            ]),
            [VoiceCallState.OnHold] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Ringing,
                VoiceCallState.Connected,
                VoiceCallState.Ending,
                VoiceCallState.Ended,
                VoiceCallState.Transferred,
                VoiceCallState.Failed,
            ]),
            [VoiceCallState.Ending] = FrozenSet.ToFrozenSet(
            [
                VoiceCallState.Ended,
                VoiceCallState.Transferred,
                VoiceCallState.Failed,
            ]),

            // Every outcome is final. A call that ended cannot start ringing again; that is a new call, and
            // reusing the session for it would merge two calls into one history.
            [VoiceCallState.Ended] = FrozenSet<VoiceCallState>.Empty,
            [VoiceCallState.Failed] = FrozenSet<VoiceCallState>.Empty,
            [VoiceCallState.NoAnswer] = FrozenSet<VoiceCallState>.Empty,
            [VoiceCallState.Rejected] = FrozenSet<VoiceCallState>.Empty,
            [VoiceCallState.Canceled] = FrozenSet<VoiceCallState>.Empty,
            [VoiceCallState.Transferred] = FrozenSet<VoiceCallState>.Empty,
        }.ToFrozenDictionary();

    private static readonly FrozenSet<VoiceCallState> _terminalStates = FrozenSet.ToFrozenSet(
    [
        VoiceCallState.Ended,
        VoiceCallState.Failed,
        VoiceCallState.NoAnswer,
        VoiceCallState.Rejected,
        VoiceCallState.Canceled,
        VoiceCallState.Transferred,
    ]);

    /// <summary>
    /// Determines whether a call session in one state may move to another.
    /// </summary>
    /// <param name="from">The state the call is in.</param>
    /// <param name="to">The state the call would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(VoiceCallState from, VoiceCallState to)
    {
        if (from == to)
        {
            return true;
        }

        return _transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Determines whether a normalized call state is terminal.
    /// </summary>
    /// <param name="state">The state to inspect.</param>
    /// <returns><see langword="true"/> when the state is terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(VoiceCallState state)
        => _terminalStates.Contains(state);
}
