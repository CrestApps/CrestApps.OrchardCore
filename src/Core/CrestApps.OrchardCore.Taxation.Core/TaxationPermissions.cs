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

    /// <summary>
    /// Grants marking an income tax rule as checked against the agency's publications. It only informs (an unchecked
    /// rule is still worked out), but people rely on it, so it is kept to the ones who check.
    /// </summary>
    public static readonly Permission VerifyIncomeTaxRules = new(
        "VerifyIncomeTaxRules",
        LocalizationSource.Create("Verify income tax rules", typeof(TaxationPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Grants marking tax returns as filed, amending them, recording their payments and filing them electronically. It
    /// grants <see cref="ManageTaxReturns"/> too.
    /// </summary>
    public static readonly Permission FileTaxReturns = new(
        "FileTaxReturns",
        LocalizationSource.Create("File tax returns", typeof(TaxationPermissions)));

    /// <summary>
    /// Grants preparing tax returns from the books, marking them ready, and seeing them.
    /// </summary>
    public static readonly Permission ManageTaxReturns = new(
        "ManageTaxReturns",
        LocalizationSource.Create("Manage tax returns", typeof(TaxationPermissions)),
        [FileTaxReturns]);
}
