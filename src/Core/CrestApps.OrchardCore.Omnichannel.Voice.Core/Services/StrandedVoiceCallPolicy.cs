namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Decides when an automated voice call that never reported its end is over.
/// </summary>
/// <remarks>
/// <para>
/// A call is concluded when the provider reports its hangup. When that report is lost -- live, the database was
/// locked as it arrived and the delivery was dropped -- the activity stayed in progress for good: nothing else ever
/// concludes an automated call, and the lead it belongs to was skipped by every later load as already having an
/// open activity. Eight more were found waiting for an answer that would never come, some for weeks.
/// </para>
/// <para>
/// A live call leaves a trace every few seconds -- each turn is stored as it is spoken -- and rings for a minute at
/// most. So a call with nothing new for <see cref="QuietFor"/> since its last turn, or since it was dialed when it
/// has no turns, is over. A call from before dial times were recorded has only its creation and schedule to go by;
/// it is left for <see cref="QuietForWithoutDialTime"/>, long enough that no call placed since could still be live.
/// </para>
/// </remarks>
public static class StrandedVoiceCallPolicy
{
    /// <summary>
    /// How long a call must have been silent, since its last turn or its dial, to be taken as over.
    /// </summary>
    public static readonly TimeSpan QuietFor = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long a call with no recorded dial time and no turns must be, from its creation and schedule, to be
    /// taken as over.
    /// </summary>
    public static readonly TimeSpan QuietForWithoutDialTime = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether the call is over.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="dialedUtc">When the call was dialed, when that was recorded.</param>
    /// <param name="lastTurnUtc">When the call's latest turn was stored, when it has any.</param>
    /// <param name="createdUtc">When the activity was created.</param>
    /// <param name="scheduledUtc">When the activity was scheduled.</param>
    /// <returns><see langword="true"/> when nothing can still be happening on the call.</returns>
    public static bool IsStranded(DateTime nowUtc, DateTime? dialedUtc, DateTime? lastTurnUtc, DateTime createdUtc, DateTime scheduledUtc)
    {
        if (dialedUtc.HasValue || lastTurnUtc.HasValue)
        {
            var lastSign = Max(dialedUtc, lastTurnUtc);

            return nowUtc - lastSign >= QuietFor;
        }

        var latest = createdUtc > scheduledUtc ? createdUtc : scheduledUtc;

        return nowUtc - latest >= QuietForWithoutDialTime;
    }

    private static DateTime Max(DateTime? first, DateTime? second)
    {
        if (!first.HasValue)
        {
            return second.Value;
        }

        if (!second.HasValue)
        {
            return first.Value;
        }

        return first.Value > second.Value ? first.Value : second.Value;
    }
}
