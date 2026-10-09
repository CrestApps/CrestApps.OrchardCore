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
    public static readonly Permission ManageAllReportDesigns = new("ManageAllReportDesigns", "Manage all custom reports and views", isSecurityCritical: true);

    /// <summary>
    /// Allows designing reports and views, and managing the ones the user owns.
    /// </summary>
    public static readonly Permission ManageOwnReportDesigns = new("ManageOwnReportDesigns", "Build reports and manage own custom reports and views", [ManageAllReportDesigns]);

    /// <summary>
    /// Allows running every designed report, whoever owns it and whoever it is shared with.
    /// </summary>
    public static readonly Permission ViewAllReportDesigns = new("ViewAllReportDesigns", "View all custom reports", [ManageAllReportDesigns]);

    /// <summary>
    /// Allows sharing designed reports with anonymous visitors and through share links. People with this permission
    /// can publish data they can read to anyone who has the link.
    /// </summary>
    public static readonly Permission ShareReportsPublicly = new("ShareReportsPublicly", "Share custom reports publicly and through share links", isSecurityCritical: true);
}
