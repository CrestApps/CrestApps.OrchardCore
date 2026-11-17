namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// The account a person is linking their bank to, and who they are.
/// </summary>
public sealed class BankAccountLinkContext
{
    /// <summary>
    /// Gets or sets the account: a new one, not saved yet, or one that exists.
    /// </summary>
    public BankAccount Account { get; set; }

    /// <summary>
    /// Gets or sets whether the account is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the id of the signed-in user.
    /// </summary>
    public string UserId { get; set; }
}
