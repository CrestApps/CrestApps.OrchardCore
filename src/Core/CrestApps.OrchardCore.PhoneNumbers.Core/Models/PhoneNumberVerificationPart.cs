using OrchardCore.ContentManagement;
using CrestApps.OrchardCore.PhoneNumbers.Verifications;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Models;

/// <summary>
/// Stores phone number verification data on a content item (for example, a Contact).
/// The full normalized provider response is retained so future providers can expose
/// additional information without requiring schema changes.
/// </summary>
public sealed class PhoneNumberVerificationPart : ContentPart
{
    /// <summary>
    /// Gets or sets the phone number submitted for verification.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number in E.164 format when available.
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent completed validity verification.
    /// </summary>
    public DateTime? LastVerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last triggered a verification.
    /// User identifiers are stored instead of usernames because usernames may change over time.
    /// </summary>
    public string LastVerifiedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider that produced the stored verification result.
    /// </summary>
    public string VerificationProvider { get; set; }

    /// <summary>
    /// Gets or sets the normalized verification status.
    /// </summary>
    public PhoneNumberVerificationStatus VerificationStatus { get; set; }

    /// <summary>
    /// Gets or sets the serialized, provider-agnostic verification result.
    /// </summary>
    public string VerificationResultJson { get; set; }

    /// <summary>
    /// Gets or sets the number of verification attempts performed for this content item.
    /// </summary>
    public int VerificationAttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the next verification becomes due.
    /// </summary>
    public DateTime? NextVerificationDueUtc { get; set; }
}
