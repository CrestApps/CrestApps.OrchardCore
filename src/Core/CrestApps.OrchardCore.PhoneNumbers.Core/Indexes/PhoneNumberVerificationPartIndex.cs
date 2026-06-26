using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Verifications;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;

/// <summary>
/// YesSql map index that exposes the commonly queried phone number verification fields
/// for reporting, dashboard widgets, revalidation jobs, and administrative searches.
/// Provider-specific metadata is intentionally left in the stored JSON payload.
/// </summary>
public sealed class PhoneNumberVerificationPartIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item identifier that owns the verification data.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the phone number submitted for verification.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized (E.164) phone number that was verified.
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is currently verified.
    /// </summary>
    public bool IsVerified { get; set; }

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
    /// Gets or sets the UTC timestamp when the next verification becomes due.
    /// </summary>
    public DateTime? NextVerificationDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the carrier name.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is a mobile line.
    /// </summary>
    public bool IsMobile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is a landline.
    /// </summary>
    public bool IsLandline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is a VoIP line.
    /// </summary>
    public bool IsVoip { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific line status when available.
    /// </summary>
    public string LineStatus { get; set; }
}
