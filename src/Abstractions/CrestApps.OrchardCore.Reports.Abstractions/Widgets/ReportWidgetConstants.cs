namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Names shared by the report dashboard widgets and the modules that ship ready-made ones.
/// </summary>
public static class ReportWidgetConstants
{
    /// <summary>
    /// The Orchard Core feature whose dashboard shows the report widgets. Reports show there when it is enabled with
    /// Reports; there is no feature of its own to turn on.
    /// </summary>
    public const string AdminDashboardFeature = "OrchardCore.AdminDashboard";

    /// <summary>
    /// The name of the content part that makes a dashboard widget show a report.
    /// </summary>
    public const string PartName = "ReportWidgetPart";

    /// <summary>
    /// The name of the widget type that shows any report its viewer may see.
    /// </summary>
    public const string GenericWidgetType = "ReportWidget";

    /// <summary>
    /// The name of the content part that makes a dashboard widget show a report or a view built in the report builder.
    /// </summary>
    public const string DesignPartName = "ReportDesignWidgetPart";

    /// <summary>
    /// The name of the widget type that shows a report or a view built in the report builder, which its viewer may see.
    /// </summary>
    public const string DesignWidgetType = "ReportDesignWidget";

    /// <summary>
    /// The stereotype of admin dashboard widget types.
    /// </summary>
    public const string DashboardWidgetStereotype = "DashboardWidget";

    /// <summary>
    /// How long, in minutes, a widget keeps what it shows before running its report again, unless it is told otherwise.
    /// </summary>
    public const int DefaultCacheMinutes = 30;
}
