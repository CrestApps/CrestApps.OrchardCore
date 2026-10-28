using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The permissions of the report builder. A designed report or view is authorized the way Orchard Core authorizes a
/// content item: the broad permission is checked with the report as the resource, and an authorization handler grants
/// it to the owner and to the people and roles the report is shared with.
/// </summary>
public static class ReportDesignerPermissions
{
    /// <summary>
    /// Allows managing every designed report and view, whoever owns it.
    /// </summary>
    public static readonly Permission ManageAllReportDesigns = new("ManageAllReportDesigns", LocalizationSource.Create("Manage all custom reports and views", typeof(ReportDesignerPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Allows designing reports and views, and managing the ones the user owns.
    /// </summary>
    public static readonly Permission ManageOwnReportDesigns = new("ManageOwnReportDesigns", LocalizationSource.Create("Build reports and manage own custom reports and views", typeof(ReportDesignerPermissions)), [ManageAllReportDesigns]);

    /// <summary>
    /// Allows running every designed report, whoever owns it and whoever it is shared with.
    /// </summary>
    public static readonly Permission ViewAllReportDesigns = new("ViewAllReportDesigns", LocalizationSource.Create("View all custom reports", typeof(ReportDesignerPermissions)), [ManageAllReportDesigns]);

    /// <summary>
    /// Allows creating, changing, and deleting the groups that designed reports are listed under. Anyone who designs
    /// reports can place a report in an existing group.
    /// </summary>
    public static readonly Permission ManageReportGroups = new("ManageReportGroups", LocalizationSource.Create("Manage report groups", typeof(ReportDesignerPermissions)), [ManageAllReportDesigns]);

    /// <summary>
    /// Allows sharing designed reports with anonymous visitors and through share links. People with this permission
    /// can publish data they can read to anyone who has the link.
    /// </summary>
    public static readonly Permission ShareReportsPublicly = new("ShareReportsPublicly", LocalizationSource.Create("Share custom reports publicly and through share links", typeof(ReportDesignerPermissions)), isSecurityCritical: true);
}
