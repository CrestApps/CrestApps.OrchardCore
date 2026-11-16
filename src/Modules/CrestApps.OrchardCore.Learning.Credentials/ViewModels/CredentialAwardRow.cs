using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// One row of the credential awards list.
/// </summary>
public class CredentialAwardRow
{
    /// <summary>
    /// Gets or sets the award.
    /// </summary>
    public CredentialAward Award { get; set; }

    /// <summary>
    /// Gets or sets the recipient's user name.
    /// </summary>
    public string RecipientName { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public CredentialAwardStatus Status { get; set; }

    /// <summary>
    /// Gets or sets whether the user may revoke it.
    /// </summary>
    public bool CanRevoke { get; set; }
}
