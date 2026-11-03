using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts.B2B;

public static class B2BAccountsPermissions
{
    /// <summary>
    /// Adds and changes accounts: their buyers, locations, terms, price lists and budgets.
    /// </summary>
    public static readonly Permission ManageB2BAccounts = new(
        "ManageB2BAccounts",
        LocalizationSource.Create("Manage B2B accounts", typeof(B2BAccountsPermissions)));
}
