using System.Collections.Immutable;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// The retry state of one outbound message, carried on the message itself so the bubble an agent sees and the
/// record the outbox retries from are the same thing. A provider that is briefly unreachable no longer costs the
/// agent their message: it is queued, retried on a widening schedule, and only marked failed once the schedule
/// is exhausted.
/// </summary>
public sealed class SmsOutboundDeliveryState
{
    /// <summary>
    /// The backoff schedule, in minutes, applied after each failed attempt.
    /// </summary>
    public static readonly ImmutableArray<int> RetryDelayMinutes = [1, 5, 15, 60];

    /// <summary>
    /// Gets or sets how many times the provider has been asked to accept this message.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next attempt is due, or <see langword="null"/> when no further attempt is
    /// scheduled (the message was accepted, or the schedule is exhausted).
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the error the last attempt reported.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets the maximum number of attempts, which is the first attempt plus one per scheduled delay.
    /// </summary>
    public static int MaxAttempts => RetryDelayMinutes.Length + 1;

    /// <summary>
    /// Determines whether another attempt is allowed after the number of attempts already made.
    /// </summary>
    /// <param name="attempts">The number of attempts already made.</param>
    /// <returns><see langword="true"/> when the schedule has an attempt left.</returns>
    public static bool CanRetry(int attempts) => attempts < MaxAttempts;

    /// <summary>
    /// Gets the delay before the attempt that follows the given number of attempts.
    /// </summary>
    /// <param name="attempts">The number of attempts already made.</param>
    /// <returns>The delay before the next attempt.</returns>
    public static TimeSpan GetDelay(int attempts)
    {
        var index = Math.Clamp(attempts - 1, 0, RetryDelayMinutes.Length - 1);

        return TimeSpan.FromMinutes(RetryDelayMinutes[index]);
    }
}
