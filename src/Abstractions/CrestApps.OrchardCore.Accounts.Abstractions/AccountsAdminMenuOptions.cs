namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// Decides whether the Accounts module adds its own "Accounts" item to the admin menu.
/// </summary>
/// <remarks>
/// A module that already lists accounts in a menu of its own, such as the CRM in the Interaction Center, turns this
/// off so the list is not shown twice.
/// </remarks>
public sealed class AccountsAdminMenuOptions
{
    /// <summary>
    /// Gets or sets whether the top-level "Accounts" item is shown. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowAccountsMenu { get; set; } = true;
}
