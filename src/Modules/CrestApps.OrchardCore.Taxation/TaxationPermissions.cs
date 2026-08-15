using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Represents the permissions exposed by the taxation module.
/// </summary>
public static class TaxationPermissions
{
    /// <summary>
    /// Grants access to manage tax categories, jurisdictions, and rules.
    /// </summary>
    public static readonly Permission ManageTaxation = new("ManageTaxation", "Manage taxation");
}
