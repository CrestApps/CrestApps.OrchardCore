using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The one account of a call's holds, shared by every writer that learns of one: the provider's call stream, which
/// reports holds the provider performs, and the agent's soft phone, which reports holds performed in the agent's own
/// media that the provider never sees.
/// </summary>
internal static class CallSessionHolds
{
    /// <summary>
    /// Puts the call on hold, remembering when the hold began. A hold already running keeps its start, so a repeated
    /// report of the same hold does not shorten it.
    /// </summary>
    /// <param name="session">The call.</param>
    /// <param name="now">When the hold began.</param>
    /// <param name="placedByAgent">Whether the agent placed the hold from the soft phone.</param>
    public static void Start(CallSession session, DateTime now, bool placedByAgent = false)
    {
        if (!session.IsOnHold || !session.HoldStartedUtc.HasValue)
        {
            session.HoldStartedUtc = now;
            session.HoldPlacedByAgent = placedByAgent;
        }
        else if (placedByAgent)
        {
            session.HoldPlacedByAgent = true;
        }

        session.IsOnHold = true;
    }

    /// <summary>
    /// Takes the call off hold, adding the hold that just ended to the call's hold time. The hold's start is kept, so
    /// the resume can say how long the hold lasted.
    /// </summary>
    /// <param name="session">The call.</param>
    /// <param name="now">When the hold ended.</param>
    /// <returns>How long the hold that ended lasted, or <see langword="null"/> when no hold was running.</returns>
    public static double? End(CallSession session, DateTime now)
    {
        double? lasted = null;

        if (session.IsOnHold && session.HoldStartedUtc.HasValue)
        {
            lasted = Math.Max(0, (now - session.HoldStartedUtc.Value).TotalSeconds);
            session.HoldSeconds += lasted.Value;
        }

        session.IsOnHold = false;
        session.HoldPlacedByAgent = false;

        return lasted;
    }

    /// <summary>
    /// The state to apply for a state the provider reported. While the agent holds a call, a provider that performs
    /// hold in the agent's media still reports it connected; that report must not end the hold, which only the agent
    /// resuming or the call ending does.
    /// </summary>
    /// <param name="session">The call as it stands.</param>
    /// <param name="reported">The state the provider reported.</param>
    /// <returns>The state to apply.</returns>
    public static VoiceCallState KeepAgentHold(CallSession session, VoiceCallState reported)
        => reported == VoiceCallState.Connected && session.IsOnHold && session.HoldPlacedByAgent
            ? VoiceCallState.OnHold
            : reported;
}
