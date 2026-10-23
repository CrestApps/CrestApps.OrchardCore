namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Names shared by the report dashboard widgets and the modules that ship ready-made ones.
/// </summary>
public static class ReportWidgetConstants
{
    /// <summary>
    /// The feature that shows reports as admin dashboard widgets.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Reports.DashboardWidgets";

    /// <summary>
    /// The name of the content part that makes a dashboard widget show a report.
    /// </summary>
    public const string PartName = "ReportWidgetPart";

    /// <summary>
    /// The name of the widget type that shows any report its viewer may see.
    /// </summary>
    public const string GenericWidgetType = "ReportWidget";

    /// <summary>
    /// The stereotype of admin dashboard widget types.
    /// </summary>
    public const string DashboardWidgetStereotype = "DashboardWidget";

    /// <summary>
    /// How long, in minutes, a widget keeps what it shows before running its report again, unless it is told otherwise.
    /// </summary>
    public const int DefaultCacheMinutes = 30;
}
