using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Companies;

public static class CompaniesPermissions
{
    /// <summary>
    /// Adds and changes companies: their buyers, locations, terms, price lists and budgets.
    /// </summary>
    public static readonly Permission ManageCompanies = new(
        "ManageCompanies",
        LocalizationSource.Create("Manage companies", typeof(CompaniesPermissions)));
}
