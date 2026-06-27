namespace CrestApps.OrchardCore.PhoneNumbers.Core.Models;

/// <summary>
/// Global site settings for the Phone Number Verifications module.
/// </summary>
public sealed class PhoneNumberVerificationsSettings
{
    /// <summary>
    /// The default revalidation interval, in days, applied when no explicit value is configured.
    /// </summary>
    public const int DefaultRevalidationIntervalDays = 365;

    /// <summary>
    /// The default maximum number of consecutive failed verification attempts before a record
    /// stops being retried automatically and is surfaced for manual attention.
    /// </summary>
    public const int DefaultMaxVerificationAttempts = 3;

    /// <summary>
    /// The default delay, in milliseconds, applied between consecutive provider verification
    /// requests so background processing does not exceed provider rate limits.
    /// </summary>
    public const int DefaultRequestDelayMilliseconds = 1000;

    /// <summary>
    /// Gets or sets the number of days after which a verified phone number must be revalidated.
    /// </summary>
    public int RevalidationIntervalDays { get; set; } = DefaultRevalidationIntervalDays;

    /// <summary>
    /// Gets or sets the maximum number of consecutive failed verification attempts before a record
    /// stops being retried automatically. Exhausted records remain visible in the verification
    /// records queue and can be re-queued manually.
    /// </summary>
    public int MaxVerificationAttempts { get; set; } = DefaultMaxVerificationAttempts;

    /// <summary>
    /// Gets or sets the delay, in milliseconds, applied between consecutive provider verification
    /// requests during background processing. A higher value spaces out provider calls to avoid
    /// rate-limit (HTTP 429) responses when many records are verified in sequence.
    /// </summary>
    public int RequestDelayMilliseconds { get; set; } = DefaultRequestDelayMilliseconds;

    /// <summary>
    /// Gets or sets the key of the provider used by default when verifying phone numbers.
    /// </summary>
    public string SelectedProvider { get; set; }
}
