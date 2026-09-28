using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Works out how many calls a predictive pass may place.
/// <para>
/// Predictive dialing places more calls than there are agents, betting that most will not be answered. When the
/// bet is wrong a real person picks up and hears nobody — the abandoned call regulators cap and fine on. This
/// calculation is therefore the safety mechanism rather than an optimisation, and it gives ground as the
/// measured rate approaches the ceiling instead of waiting for the ceiling to be breached: a breach has already
/// happened to real callers by the time it is measurable, and a rolling rate falls far more slowly than it rises.
/// </para>
/// </summary>
public static class PredictiveDialerPacing
{
    /// <summary>
    /// The most calls per agent any predictive profile may place, however it is configured. A profile asking for
    /// more is a misconfiguration, and honouring it would abandon calls faster than any rolling measurement
    /// could notice.
    /// </summary>
    public const int MaxCallsPerAgent = 3;

    /// <summary>
    /// The fraction of the configured cap at which throttling begins. Below it the profile's ratio applies in
    /// full; above it the ratio is reduced toward one call per agent, reaching it at the cap.
    /// </summary>
    public const double ThrottleThreshold = 0.5;

    /// <summary>
    /// Calculates how many calls may be placed this pass.
    /// </summary>
    /// <param name="profile">The dialer profile being run.</param>
    /// <param name="availableAgents">How many agents are free to take a connected call.</param>
    /// <param name="measuredAbandonmentPercent">The campaign's rolling abandonment rate.</param>
    /// <returns>The number of calls to place; zero when none may be.</returns>
    public static int CalculatePace(DialerProfile profile, int availableAgents, double measuredAbandonmentPercent)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Dialing with nobody to connect to guarantees an abandoned call for every answer.
        if (availableAgents < 1)
        {
            return 0;
        }

        // A predictive profile with no cap has no safety mechanism at all, so it gets the one pacing that
        // cannot abandon rather than the benefit of the doubt.
        if (!profile.EnforceAbandonmentCap || profile.MaxAbandonmentRatePercent <= 0)
        {
            return availableAgents;
        }

        var ratio = Math.Clamp(profile.CallsPerAgent, 1, MaxCallsPerAgent);

        if (ratio <= 1)
        {
            return availableAgents;
        }

        var cap = profile.MaxAbandonmentRatePercent;
        var headroomStart = cap * ThrottleThreshold;

        // At or past the cap, stop over-dialing entirely: one call per agent cannot abandon, because there is
        // somebody waiting for every call it places.
        if (measuredAbandonmentPercent >= cap)
        {
            return availableAgents;
        }

        if (measuredAbandonmentPercent <= headroomStart)
        {
            return availableAgents * ratio;
        }

        // Between the threshold and the cap the extra calls per agent are scaled down linearly, so the dialer
        // eases off as the rate climbs rather than falling off a cliff at the cap.
        var remaining = (cap - measuredAbandonmentPercent) / (cap - headroomStart);
        var extraPerAgent = (ratio - 1) * remaining;
        var effectiveRatio = 1d + extraPerAgent;

        return Math.Max(availableAgents, (int)Math.Floor(availableAgents * effectiveRatio));
    }
}
