using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// One credential on the My credentials page.
/// </summary>
public class MyCredentialEntry
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
    /// Gets or sets the address of the public verification page to share.
    /// </summary>
    public string ShareUrl { get; set; }
}
