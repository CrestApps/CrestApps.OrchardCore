namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the phone number verification report dashboard.
/// </summary>
public class PhoneNumberVerificationReportViewModel
{
    /// <summary>
    /// Gets or sets the total number of contacts that carry the verification part.
    /// </summary>
    public int TotalContacts { get; set; }

    /// <summary>
    /// Gets or sets the number of verified phone numbers.
    /// </summary>
    public int VerifiedNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of unverified phone numbers.
    /// </summary>
    public int UnverifiedNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of invalid phone numbers.
    /// </summary>
    public int InvalidNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of mobile phone numbers.
    /// </summary>
    public int MobileNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of landline phone numbers.
    /// </summary>
    public int LandlineNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of VoIP phone numbers.
    /// </summary>
    public int VoipNumbers { get; set; }

    /// <summary>
    /// Gets or sets the number of contacts that have never been verified.
    /// </summary>
    public int PendingVerification { get; set; }

    /// <summary>
    /// Gets or sets the number of contacts whose verification has expired.
    /// </summary>
    public int RequiringRevalidation { get; set; }

    /// <summary>
    /// Gets or sets the number of verification failures.
    /// </summary>
    public int VerificationFailures { get; set; }

    /// <summary>
    /// Gets or sets the verification success rate as a percentage between 0 and 100.
    /// </summary>
    public double VerificationSuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the usage counts per provider.
    /// </summary>
    public IDictionary<string, int> ProviderUsageCounts { get; set; } = new Dictionary<string, int>();
}
