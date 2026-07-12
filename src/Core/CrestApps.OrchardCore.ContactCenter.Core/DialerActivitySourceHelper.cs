using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Resolves the activity source classification for a dialer mode.
/// </summary>
public static class DialerActivitySourceHelper
{
    /// <summary>
    /// Gets the activity source identifier that corresponds to the specified dialer mode.
    /// </summary>
    /// <param name="mode">The dialer mode.</param>
    /// <returns>The activity source identifier used to classify activities created for the dialer mode.</returns>
    public static string GetActivitySource(DialerMode mode)
    {
        return mode switch
        {
            DialerMode.Power => ActivitySources.PowerDial,
            DialerMode.Progressive => ActivitySources.ProgressiveDial,
            DialerMode.Predictive => ActivitySources.PredictiveDial,
            _ => ActivitySources.PreviewDial,
        };
    }

    /// <summary>
    /// Determines whether an activity source represents agent-facing dialer work.
    /// </summary>
    /// <param name="source">The activity source identifier.</param>
    /// <returns><see langword="true"/> when the source is a dialer source; otherwise, <see langword="false"/>.</returns>
    public static bool IsDialerSource(string source)
    {
        return string.Equals(source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, ActivitySources.PreviewDial, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, ActivitySources.PowerDial, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, ActivitySources.ProgressiveDial, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, ActivitySources.PredictiveDial, StringComparison.OrdinalIgnoreCase);
    }
}
