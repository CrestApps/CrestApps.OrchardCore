using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Companies;

public static class CompaniesPermissions
{
    /// <summary>
    /// Adds and changes companies: their buyers, locations, terms, price lists and budgets.
    /// </summary>
    public static readonly Permission ManageCompanies = new("ManageCompanies", "Manage companies");
}

internal sealed class CompaniesPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CompaniesPermissions.ManageCompanies,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
