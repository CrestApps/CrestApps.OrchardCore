namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Thrown by an <see cref="IOmnichannelProcessor"/> that cannot start an activity yet but will be able to later: its
/// sending address is at its limit, or paused. The caller reschedules the activity for <see cref="RetryAtUtc"/>
/// without counting a failed attempt, because nothing failed.
/// </summary>
public sealed class OmnichannelActivityDeferredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityDeferredException"/> class.
    /// </summary>
    /// <param name="retryAtUtc">When the activity may be started.</param>
    /// <param name="message">Why it was held back.</param>
    public OmnichannelActivityDeferredException(DateTime retryAtUtc, string message)
        : base(message)
    {
        RetryAtUtc = retryAtUtc;
    }

    /// <summary>
    /// Gets when the activity may be started.
    /// </summary>
    public DateTime RetryAtUtc { get; }
}
