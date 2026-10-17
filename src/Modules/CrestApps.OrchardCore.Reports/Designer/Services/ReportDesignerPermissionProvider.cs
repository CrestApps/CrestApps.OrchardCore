using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

internal sealed class ReportDesignerPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions =
    [
        ReportDesignerPermissions.ManageAllReportDesigns,
        ReportDesignerPermissions.ManageOwnReportDesigns,
        ReportDesignerPermissions.ViewAllReportDesigns,
        ReportDesignerPermissions.ShareReportsPublicly,
    ];

    /// <summary>
    /// Retrieves the permissions of the report builder.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult(_permissions);
    }

    /// <summary>
    /// Grants every report builder permission to administrators.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _permissions,
            },
        ];
    }
}
