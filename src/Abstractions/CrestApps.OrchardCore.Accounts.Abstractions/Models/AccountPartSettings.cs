namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The settings of the account part on an account type.
/// </summary>
public sealed class AccountPartSettings
{
    /// <summary>
    /// Gets or sets the content types that have been offered in the account's list. A type is offered once, the first
    /// time it is seen, so an administrator who takes it out of the list keeps it out.
    /// </summary>
    public string[] OfferedContentTypes { get; set; } = [];
}
