using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Banking;

/// <summary>
/// The Banking permissions.
/// </summary>
public static class BankingPermissions
{
    /// <summary>
    /// Add, change, verify and close every bank account. Full account numbers stay hidden without
    /// <see cref="RevealBankAccountNumbers"/>.
    /// </summary>
    public static readonly Permission ManageBankAccounts = new("ManageBankAccounts", LocalizationSource.Create("Manage bank accounts", typeof(BankingPermissions)));

    /// <summary>
    /// Add and change one's own bank accounts: those whose owner is the signed-in user, or a party the user is, such as
    /// their staff record.
    /// </summary>
    public static readonly Permission ManageOwnBankAccounts = new("ManageOwnBankAccounts", LocalizationSource.Create("Manage one's own bank accounts", typeof(BankingPermissions)), [ManageBankAccounts]);

    /// <summary>
    /// See full bank account numbers. Every reveal is written to the Audit Trail when it is on.
    /// </summary>
    public static readonly Permission RevealBankAccountNumbers = new("RevealBankAccountNumbers", LocalizationSource.Create("Reveal bank account numbers", typeof(BankingPermissions)), isSecurityCritical: true);
}
