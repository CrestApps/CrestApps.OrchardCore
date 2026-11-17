using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Banking.Disbursements;

/// <summary>
/// The Disbursements permissions.
/// </summary>
public static class DisbursementsPermissions
{
    /// <summary>
    /// Record, batch, release, settle and cancel disbursements, download bank files and import returns.
    /// </summary>
    public static readonly Permission ManageDisbursements = new("ManageDisbursements", LocalizationSource.Create("Manage disbursements", typeof(DisbursementsPermissions)), isSecurityCritical: true);

    /// <summary>
    /// See disbursements and batches.
    /// </summary>
    public static readonly Permission ViewDisbursements = new("ViewDisbursements", LocalizationSource.Create("View disbursements", typeof(DisbursementsPermissions)), [ManageDisbursements]);
}
