using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// An award's admin page.
/// </summary>
public class CredentialAwardDetailViewModel
{
    /// <summary>
    /// Gets or sets the award.
    /// </summary>
    public CredentialAward Award { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public CredentialAwardStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the check of the credential.
    /// </summary>
    public CredentialVerificationResult Verification { get; set; }

    /// <summary>
    /// Gets or sets the recipient's name.
    /// </summary>
    public string RecipientName { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who awarded it by hand.
    /// </summary>
    public string AwardedByName { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who revoked it.
    /// </summary>
    public string RevokedByName { get; set; }

    /// <summary>
    /// Gets or sets the address of the public verification page.
    /// </summary>
    public string VerifyUrl { get; set; }

    /// <summary>
    /// Gets or sets the credential as issued, indented.
    /// </summary>
    public string CredentialJson { get; set; }

    /// <summary>
    /// Gets or sets whether the user may revoke it.
    /// </summary>
    public bool CanRevoke { get; set; }
}
