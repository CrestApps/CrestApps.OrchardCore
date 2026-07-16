namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Defines the server-owned execution deadline for telephony provider mutations.
/// </summary>
public sealed class TelephonyCommandOptions
{
    /// <summary>
    /// The default provider mutation timeout, in seconds.
    /// </summary>
    public const int DefaultTimeoutSeconds = 10;

    /// <summary>
    /// The minimum provider mutation timeout, in seconds.
    /// </summary>
    public const int MinimumTimeoutSeconds = 1;

    /// <summary>
    /// The maximum provider mutation timeout, in seconds.
    /// </summary>
    public const int MaximumTimeoutSeconds = 120;

    /// <summary>
    /// Gets or sets the maximum duration of one telephony provider mutation before its outcome is
    /// treated as unknown. This outer command deadline intentionally supersedes longer
    /// provider-specific retry budgets.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
}
