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
    /// Gets a value indicating whether the mode requires the Contact Center Automated Dialer feature to be
    /// enabled before a profile may use it. Predictive is excluded because it is disabled entirely regardless
    /// of the feature state.
    /// </summary>
    /// <param name="mode">The dialer mode to classify.</param>
    /// <returns><see langword="true"/> for Power and Progressive; otherwise <see langword="false"/>.</returns>
    public static bool RequiresAutomatedDialerFeature(this DialerMode mode)
    {
        return mode is DialerMode.Power or DialerMode.Progressive;
    }
}
