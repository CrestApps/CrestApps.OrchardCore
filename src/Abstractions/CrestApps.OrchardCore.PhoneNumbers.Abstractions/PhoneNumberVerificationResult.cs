using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Represents a provider-agnostic phone number verification result.
/// Providers map their native responses into this common shape so that consumers
/// never depend on a specific verification provider.
/// </summary>
public sealed class PhoneNumberVerificationResult
{
    /// <summary>
    /// Gets or sets the phone number that was submitted for verification.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the normalized phone number in E.164 format (e.g., <c>+17024993350</c>).
    /// </summary>
    public string NormalizedPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the national display format when the provider supplies one.
    /// </summary>
    public string NationalFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is reachable.
    /// </summary>
    public bool IsReachable { get; set; }

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
    /// Gets or sets the ISO 3166-1 alpha-2 country code (e.g., <c>US</c>).
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the human-readable country name (e.g., <c>United States</c>).
    /// </summary>
    public string CountryName { get; set; }

    /// <summary>
    /// Gets or sets the international dialing prefix for the country (e.g., <c>+1</c>).
    /// </summary>
    public string CountryPrefix { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported region or state.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the carrier name associated with the phone number.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone identifier associated with the phone number.
    /// </summary>
    public string TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the normalized line type reported for the phone number.
    /// </summary>
    public PhoneNumberLineType LineType { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific line status when available (for example, <c>active</c>).
    /// </summary>
    public string LineStatus { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific minimum observed line age when available.
    /// </summary>
    public string MinimumAge { get; set; }

    /// <summary>
    /// Gets or sets an optional provider risk score, where higher values indicate higher risk.
    /// </summary>
    public double? RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the provider risk level when available (for example, <c>low</c>).
    /// </summary>
    public string RiskLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider identified the number as disposable.
    /// </summary>
    public bool? IsDisposable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider detected abuse signals.
    /// </summary>
    public bool? IsAbuseDetected { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider that produced this result.
    /// </summary>
    public string VerificationProvider { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific reference identifier for this verification, when available.
    /// </summary>
    public string ProviderReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the verification was performed.
    /// </summary>
    public DateTime VerificationDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the raw, unmodified provider response payload.
    /// </summary>
    public string RawProviderResponse { get; set; }

    /// <summary>
    /// Gets or sets the normalized verification status derived from the provider response.
    /// </summary>
    public PhoneNumberVerificationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a human-readable error message describing why a verification request failed.
    /// This is populated when the request itself could not be completed (for example, a provider
    /// rate limit, an HTTP error, or a transport failure) rather than when a number is genuinely invalid.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets the provider-extensible metadata bag for values that are not part of the common model.
    /// </summary>
    public JsonObject Metadata { get; init; } = [];
}
