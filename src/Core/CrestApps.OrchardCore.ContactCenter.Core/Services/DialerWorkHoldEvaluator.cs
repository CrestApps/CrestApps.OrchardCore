using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides whether a waiting campaign record may be dialed yet. The queue gate acts on the answer, and the dispatch
/// diagnostics report it, so what a supervisor reads is the decision the dispatcher made rather than a copy of it.
/// </summary>
internal static class DialerWorkHoldEvaluator
{
    /// <summary>
    /// Decides whether the dialer profile itself holds its records back.
    /// </summary>
    /// <param name="profile">The dialer profile the record is dialed under.</param>
    /// <returns><see cref="DispatchBlocker.ProfileDisabled"/> when the profile is turned off; otherwise
    /// <see cref="DispatchBlocker.DialableNow"/>.</returns>
    public static DispatchBlocker EvaluateProfile(DialerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.Enabled ? DispatchBlocker.DialableNow : DispatchBlocker.ProfileDisabled;
    }

    /// <summary>
    /// Decides whether a record of an enabled dialer profile has used its attempts, is scheduled for later, or is cooling
    /// down after its last call.
    /// </summary>
    /// <param name="profile">The dialer profile the record is dialed under.</param>
    /// <param name="activity">The record.</param>
    /// <param name="nextAttempt">The number of the attempt the next dial would be.</param>
    /// <param name="lastEndedUtc">When the record's last call ended, or <see langword="null"/> when it has had none.</param>
    /// <param name="lastCallAnswered">Whether the record's last call reached an agent, which leaves the attempt limit to
    /// the dial itself.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The reason the record is held back and when it becomes due, or <see cref="DispatchBlocker.DialableNow"/>.</returns>
    public static (DispatchBlocker Blocker, DateTime? DueUtc) EvaluateRecord(
        DialerProfile profile,
        OmnichannelActivity activity,
        int nextAttempt,
        DateTime? lastEndedUtc,
        bool lastCallAnswered,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(activity);

        if (nextAttempt > DialerAttemptLimit.Resolve(activity, profile))
        {
            // A record whose last call reached an agent is the agent's work; the attempt limit is left to the dial itself.
            return lastCallAnswered
                ? (DispatchBlocker.DialableNow, null)
                : (DispatchBlocker.AttemptsExhausted, null);
        }

        // A record scheduled for later -- a follow-up the subject's "Try again" action or a workflow set to call back
        // after the retry delay -- and one still inside the retry cool-down after its last call wait their turn.
        var blocker = DispatchBlocker.DialableNow;
        DateTime? dueUtc = null;

        if (activity.ScheduledUtc > utcNow)
        {
            blocker = DispatchBlocker.ScheduledForLater;
            dueUtc = activity.ScheduledUtc;
        }

        if (profile.RetryDelayMinutes > 0 && lastEndedUtc is { } endedUtc)
        {
            var coolDownEndsUtc = endedUtc.AddMinutes(profile.RetryDelayMinutes);

            if (coolDownEndsUtc > utcNow && (dueUtc is null || coolDownEndsUtc > dueUtc))
            {
                blocker = DispatchBlocker.RetryCoolDown;
                dueUtc = coolDownEndsUtc;
            }
        }

        return (blocker, dueUtc);
    }
}
