using CrestApps.OrchardCore.Learning.Credentials.Core.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The public verification page of a credential.
/// </summary>
public class CredentialVerificationViewModel
{
    /// <summary>
    /// Gets or sets what the check found.
    /// </summary>
    public CredentialVerificationOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets whether the signature verified.
    /// </summary>
    public bool SignatureValid { get; set; }

    /// <summary>
    /// Gets or sets whether the recipient's personal data was erased.
    /// </summary>
    public bool Erased { get; set; }

    /// <summary>
    /// Gets or sets the achievement's name.
    /// </summary>
    public string AchievementName { get; set; }

    /// <summary>
    /// Gets or sets the achievement's description.
    /// </summary>
    public string AchievementDescription { get; set; }

    /// <summary>
    /// Gets or sets the criteria narrative.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the image address.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the achievement type.
    /// </summary>
    public string AchievementType { get; set; }

    /// <summary>
    /// Gets or sets the issuer's name.
    /// </summary>
    public string IssuerName { get; set; }

    /// <summary>
    /// Gets or sets the recipient's name, only when the recipient chose to show it.
    /// </summary>
    public string RecipientName { get; set; }

    /// <summary>
    /// Gets or sets when it was issued, in UTC.
    /// </summary>
    public DateTime? IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it stops being valid, in UTC.
    /// </summary>
    public DateTime? ValidUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was revoked, in UTC.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Gets or sets the address of the credential in JSON-LD.
    /// </summary>
    public string CredentialUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the signed credential.
    /// </summary>
    public string JwtUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the published key set.
    /// </summary>
    public string KeySetUrl { get; set; }
}
