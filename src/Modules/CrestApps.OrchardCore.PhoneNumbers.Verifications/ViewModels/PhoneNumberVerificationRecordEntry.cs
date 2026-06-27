namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// Represents a single row in the phone number verification records queue.
/// </summary>
public class PhoneNumberVerificationRecordEntry
{
    /// <summary>
    /// Gets or sets the identifier of the content item that owns the verification data.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the display text of the content item that owns the verification data.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the content type of the content item that owns the verification data.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the phone number submitted for verification.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized (E.164) phone number.
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized verification status.
    /// </summary>
    public PhoneNumberVerificationStatus VerificationStatus { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider that produced the stored result.
    /// </summary>
    public string VerificationProvider { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent completed validity verification.
    /// </summary>
    public DateTime? LastVerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent verification attempt, successful or failed.
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the next verification becomes due.
    /// </summary>
    public DateTime? NextVerificationDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the total number of verification attempts performed.
    /// </summary>
    public int VerificationAttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failed verification requests.
    /// </summary>
    public int FailedAttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the error message from the most recent failed verification request, if any.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the record has reached the maximum failed attempts
    /// and is no longer retried automatically.
    /// </summary>
    public bool IsExhausted { get; set; }
}
