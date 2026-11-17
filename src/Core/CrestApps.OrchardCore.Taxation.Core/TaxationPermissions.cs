using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation.Core;

/// <summary>
/// Represents the permissions exposed by the taxation module.
/// </summary>
public static class TaxationPermissions
{
    /// <summary>
    /// Grants access to manage tax categories, jurisdictions, and rules.
    /// </summary>
    public static readonly Permission ManageTaxation = new(
        "ManageTaxation",
        LocalizationSource.Create("Manage taxation", typeof(TaxationPermissions)));

    /// <summary>
    /// Grants access to manage income tax rules: the rates, brackets, contributions and their yearly figures every
    /// payer's income tax is worked out with.
    /// </summary>
    public static readonly Permission ManageIncomeTaxRules = new(
        "ManageIncomeTaxRules",
        LocalizationSource.Create("Manage income tax rules", typeof(TaxationPermissions)),
        isSecurityCritical: true);
}
