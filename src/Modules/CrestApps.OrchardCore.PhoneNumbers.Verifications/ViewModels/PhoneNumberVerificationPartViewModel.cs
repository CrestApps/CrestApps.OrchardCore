namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model used to display the stored verification data on a content item editor.
/// </summary>
public class PhoneNumberVerificationPartViewModel
{
    /// <summary>
    /// Gets or sets the normalized verification status.
    /// </summary>
    public PhoneNumberVerificationStatus VerificationStatus { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider that produced the stored result.
    /// </summary>
    public string VerificationProvider { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent successful verification.
    /// </summary>
    public DateTime? LastVerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the next verification becomes due.
    /// </summary>
    public DateTime? NextVerificationDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of verification attempts performed.
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
    /// Gets or sets the UTC timestamp of the most recent verification attempt, successful or failed.
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number from the stored result.
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the carrier from the stored result.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets the line type from the stored result.
    /// </summary>
    public PhoneNumberLineType LineType { get; set; }
}
