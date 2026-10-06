namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Provides helpers that classify <see cref="DialerMode"/> values so pacing, compliance, and feature-gating
/// decisions share one authoritative definition.
/// </summary>
public static class DialerModeExtensions
{
    /// <summary>
    /// Gets a value indicating whether the mode is an automated pacing mode that dials without an agent
    /// initiating each call and can therefore abandon a connected party.
    /// </summary>
    /// <param name="mode">The dialer mode to classify.</param>
    /// <returns><see langword="true"/> for Power, Progressive, and Predictive; otherwise <see langword="false"/>.</returns>
    public static bool IsAutomated(this DialerMode mode)
    {
        return mode is DialerMode.Power or DialerMode.Progressive or DialerMode.Predictive;
    }

    /// <summary>
    /// Gets a value indicating whether the mode requires the Contact Center Paced Dialing feature to be
    /// enabled before a profile may use it. That feature offers all three system-paced modes; what a Predictive
    /// profile may do beyond one call per reserved agent is governed by the profile's own pacing model and safeguards.
    /// </summary>
    /// <param name="mode">The dialer mode to classify.</param>
    /// <returns><see langword="true"/> for Power, Progressive, and Predictive; otherwise <see langword="false"/>.</returns>
    public static bool RequiresPacedDialerFeature(this DialerMode mode)
    {
        return mode is DialerMode.Power or DialerMode.Progressive or DialerMode.Predictive;
    }
}
