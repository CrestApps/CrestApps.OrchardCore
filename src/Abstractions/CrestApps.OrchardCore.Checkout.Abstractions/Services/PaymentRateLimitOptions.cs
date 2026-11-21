namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Options for the fixed-window <see cref="IPaymentAttemptLimiter"/>.
/// </summary>
[Obsolete("Configure attempt policies by name under CrestApps_Throttling:AttemptPolicies. This type is removed in the next release.")]
public sealed class PaymentRateLimitOptions
{
    /// <summary>
    /// The maximum number of attempts allowed in a window. A non-positive value disables throttling.
    /// </summary>
    public int PermitLimit { get; set; } = 20;

    /// <summary>
    /// The length of the fixed window.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);
}
