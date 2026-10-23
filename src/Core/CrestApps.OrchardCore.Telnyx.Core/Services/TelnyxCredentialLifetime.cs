namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// How long a browser SIP credential may live.
/// </summary>
/// <remarks>
/// The soft phone renews its credential once it is within ten minutes of expiry. A lifetime of ten minutes or less was
/// therefore "expiring" the moment it was issued, and only a little longer renewed every few minutes: each renewal
/// tears the registration down and registers a new credential, and a leg rung meanwhile is refused on the one being
/// replaced (SIP 480). The settings screen accepted any lifetime of a minute or more, so the minimum is enforced
/// wherever a lifetime is read.
/// </remarks>
public static class TelnyxCredentialLifetime
{
    /// <summary>
    /// The lifetime used when none is set: three hours, so a long call is unlikely to outlast its credential.
    /// </summary>
    public const int DefaultMinutes = 180;

    /// <summary>
    /// The shortest lifetime allowed, well clear of the soft phone's ten-minute renewal window.
    /// </summary>
    public const int MinimumMinutes = 30;

    /// <summary>
    /// The lifetime to use for <paramref name="minutes"/>: the default when none is set, and never less than the minimum.
    /// </summary>
    /// <param name="minutes">The configured lifetime in minutes.</param>
    /// <returns>The lifetime in minutes.</returns>
    public static int Normalize(int minutes)
        => minutes <= 0 ? DefaultMinutes : Math.Max(minutes, MinimumMinutes);
}
