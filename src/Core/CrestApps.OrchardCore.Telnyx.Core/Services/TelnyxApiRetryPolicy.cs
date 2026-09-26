using System.Net;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Decides whether a failed Telnyx call is worth trying again, and how long to wait first.
/// <para>
/// Telnyx rate limits per account, so a busy tenant meets 429 during a burst of call control. None of the four
/// former HTTP call sites retried, which meant a rate-limited answer or bridge simply did not happen and the
/// customer heard silence. Equally, a retry is only safe when repeating the command cannot repeat its effect:
/// answering a call twice is harmless, placing one twice dials the customer twice.
/// </para>
/// </summary>
public sealed class TelnyxApiRetryPolicy
{
    /// <summary>
    /// How many times a retryable command is attempted in total.
    /// </summary>
    public const int MaxAttempts = 3;

    private readonly TimeSpan _baseDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxApiRetryPolicy"/> class.
    /// </summary>
    /// <param name="baseDelay">The delay before the first retry; it doubles on each subsequent attempt.</param>
    public TelnyxApiRetryPolicy(TimeSpan baseDelay)
    {
        _baseDelay = baseDelay;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxApiRetryPolicy"/> class with the shipped delay.
    /// </summary>
    public TelnyxApiRetryPolicy()
        : this(TimeSpan.FromMilliseconds(250))
    {
    }

    /// <summary>
    /// Determines whether a status is worth another attempt. A 4xx other than 429 means the provider understood
    /// and refused, and retrying a refusal only spends the rate-limit budget the next real command needs.
    /// </summary>
    /// <param name="statusCode">The status the provider answered with.</param>
    public static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.RequestTimeout
            || (int)statusCode >= 500;

    /// <summary>
    /// Returns how long to wait before the given attempt, honouring the provider's own <c>Retry-After</c> when it
    /// sent one — it knows when its limit resets and this code does not.
    /// </summary>
    /// <param name="attempt">The one-based attempt that just failed.</param>
    /// <param name="retryAfter">The provider's <c>Retry-After</c>, when it sent one.</param>
    public TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is not null && retryAfter.Value > TimeSpan.Zero)
        {
            return retryAfter.Value;
        }

        return _baseDelay <= TimeSpan.Zero
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(_baseDelay.Ticks * (1L << Math.Max(0, attempt - 1)));
    }
}
