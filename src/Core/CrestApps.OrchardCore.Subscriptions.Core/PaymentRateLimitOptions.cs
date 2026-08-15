namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Options controlling how many payment attempts an anonymous client may make within a rolling
/// window before being throttled. Guards the anonymous Stripe payment endpoints against automated
/// abuse such as card testing.
/// </summary>
public sealed class PaymentRateLimitOptions
{
    /// <summary>
    /// The maximum number of attempts allowed per <see cref="Window"/> for a given client/session.
    /// </summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// The length of the fixed window over which <see cref="PermitLimit"/> is counted.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
