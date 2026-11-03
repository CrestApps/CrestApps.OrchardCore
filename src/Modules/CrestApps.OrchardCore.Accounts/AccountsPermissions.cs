using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// The permissions of the Accounts features.
/// </summary>
public static class AccountsPermissions
{
    /// <summary>
    /// Chooses the users who sign in for an account and what each of them may do for it.
    /// </summary>
    public static readonly Permission ManageAccountMembers = new(
        "ManageAccountMembers",
        LocalizationSource.Create("Manage the users who sign in for accounts", typeof(AccountsPermissions)));
}
