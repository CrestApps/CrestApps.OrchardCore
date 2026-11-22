namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// How the checks of one of the business's bank accounts print, kept in the account's properties so it travels with
/// the account (as <c>BankAccountCheckSettings</c> in the Bank accounts recipe step).
/// </summary>
public sealed class BankAccountCheckSettings
{
    /// <summary>
    /// Gets or sets the check layout the account's checks print with when the person printing doesn't choose one, such
    /// as the layout of the pre-printed stock loaded for the account.
    /// </summary>
    public string DefaultLayoutId { get; set; }
}
