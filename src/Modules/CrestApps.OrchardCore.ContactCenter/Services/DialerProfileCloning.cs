using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What a copy of a dialer profile does not inherit from its source. The Dialer Profiles page and the AI dialer profile
/// tool both apply it when they copy a profile.
/// </summary>
internal static class DialerProfileCloning
{
    /// <summary>
    /// Gets whether a copy of the profile must start with no connect wait: a predictive profile's connect wait is
    /// justified only by the connected calls that profile measured, and a copy has measured none.
    /// </summary>
    /// <param name="source">The profile being copied.</param>
    /// <returns><see langword="true"/> when the copy's connect wait is reset.</returns>
    public static bool RequiresConnectWaitReset(DialerProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Mode == DialerMode.Predictive && source.ConnectWaitMilliseconds > 0;
    }

    /// <summary>
    /// Clears from a copy what only its source measured.
    /// </summary>
    /// <param name="clone">The copy.</param>
    /// <param name="source">The profile being copied.</param>
    /// <returns><see langword="true"/> when something was cleared.</returns>
    public static bool ResetMeasuredSettings(DialerProfile clone, DialerProfile source)
    {
        ArgumentNullException.ThrowIfNull(clone);

        if (!RequiresConnectWaitReset(source))
        {
            return false;
        }

        clone.ConnectWaitMilliseconds = 0;

        return true;
    }
}
