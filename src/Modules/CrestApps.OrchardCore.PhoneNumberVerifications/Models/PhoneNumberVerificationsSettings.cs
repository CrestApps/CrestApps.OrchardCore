namespace CrestApps.OrchardCore.PhoneNumberVerifications.Models;

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
    /// Gets or sets a value indicating whether just-in-time verification is enabled.
    /// When enabled, trusted operations verify a number on demand if its verification has expired.
    /// </summary>
    public bool EnableJustInTimeVerification { get; set; }

    /// <summary>
    /// Gets or sets the number of days after which a verified phone number must be revalidated.
    /// </summary>
    public int RevalidationIntervalDays { get; set; } = DefaultRevalidationIntervalDays;

    /// <summary>
    /// Gets or sets the key of the provider used by default when verifying phone numbers.
    /// </summary>
    public string SelectedProvider { get; set; }
}
