using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// What an Open Badges 3.0 credential is built from.
/// </summary>
public sealed class OpenBadgeCredentialInput
{
    /// <summary>
    /// Gets or sets the site's base address, without a trailing slash.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the issuer's name.
    /// </summary>
    public string IssuerName { get; set; }

    /// <summary>
    /// Gets or sets the award's public code.
    /// </summary>
    public string PublicId { get; set; }

    /// <summary>
    /// Gets or sets the achievement, with its address.
    /// </summary>
    public CredentialAchievement Achievement { get; set; }

    /// <summary>
    /// Gets or sets how the credential names the recipient.
    /// </summary>
    public CredentialRecipientIdentity Recipient { get; set; }

    /// <summary>
    /// Gets or sets when the credential starts to be valid, in UTC.
    /// </summary>
    public DateTime ValidFromUtc { get; set; }

    /// <summary>
    /// Gets or sets when the credential stops being valid, in UTC, when it expires.
    /// </summary>
    public DateTime? ValidUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of the status list.
    /// </summary>
    public int StatusListNumber { get; set; }

    /// <summary>
    /// Gets or sets the index in the status list.
    /// </summary>
    public int StatusListIndex { get; set; }
}
