using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Agreements;

/// <summary>
/// The permissions of the Agreements module.
/// </summary>
public static class AgreementsPermissions
{
    /// <summary>
    /// Create, change and remove agreements.
    /// </summary>
    public static readonly Permission ManageAgreements = new("ManageAgreements", LocalizationSource.Create("Manage agreements with accounts", typeof(AgreementsPermissions)));

    /// <summary>
    /// See agreements and their hours.
    /// </summary>
    public static readonly Permission ViewAgreements = new("ViewAgreements", LocalizationSource.Create("View agreements with accounts and their hours", typeof(AgreementsPermissions)), [ManageAgreements]);
}
