namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Contains constant values shared across the Reports module and the modules that contribute reports.
/// </summary>
public static class ReportsConstants
{
    /// <summary>
    /// The identifier of the Reports framework feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Reports";

    /// <summary>
    /// The identifier of the optional Open XML export feature for reports.
    /// </summary>
    public const string OpenXmlFeature = "CrestApps.OrchardCore.Reports.OpenXml";

    /// <summary>
    /// The identifier of the report builder feature, which lets people build reports from data sources with drag and
    /// drop, save reusable views, and share reports.
    /// </summary>
    public const string BuilderFeature = "CrestApps.OrchardCore.Reports.Builder";

    /// <summary>
    /// The technical name of the data source that exposes saved report views as data sets.
    /// </summary>
    public const string ViewsDataSource = "ReportViews";

    /// <summary>
    /// The kind of the shared links of Orchard Core that open a designed report.
    /// </summary>
    public const string SharedLinkKind = "Reports.Report";

    /// <summary>
    /// The technical name of Orchard Core's data source of the users of the site, which data sets reference to join
    /// their user identifiers to the users.
    /// </summary>
    public const string UsersDataSource = "Users";

    /// <summary>
    /// The technical name of the users data set of Orchard Core's users data source.
    /// </summary>
    public const string UsersDataSet = "Users";

    /// <summary>
    /// The technical name of the identifier field of Orchard Core's users data set.
    /// </summary>
    public const string UserIdField = "UserId";

    /// <summary>
    /// The priority of the top-level Reports admin menu item built by the Reports module. Orchard Core merges menu items
    /// with the same text and keeps the identifier, classes, and position of the one with the highest priority, so other
    /// modules that add items under Reports use a lower priority and the Reports item keeps its <c>reports</c>
    /// identifier, which selects its icon.
    /// </summary>
    public const int AdminMenuPriority = 2;

    /// <summary>
    /// The technical name of the built-in CSV export format.
    /// </summary>
    public const string CsvExportFormat = "csv";

    /// <summary>
    /// The technical name of the Open XML Excel workbook export format.
    /// </summary>
    public const string XlsxExportFormat = "xlsx";

    /// <summary>
    /// Contains the well-known report category names used to group reports in the admin navigation.
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// The executive report category.
        /// </summary>
        public const string Executive = "Executive";

        /// <summary>
        /// The operations report category.
        /// </summary>
        public const string Operations = "Operations";

        /// <summary>
        /// The queue and routing report category.
        /// </summary>
        public const string QueueRouting = "Queue & Routing";

        /// <summary>
        /// The agent performance report category.
        /// </summary>
        public const string AgentPerformance = "Agent Performance";

        /// <summary>
        /// The workforce and payroll report category.
        /// </summary>
        public const string WorkforcePayroll = "Workforce & Payroll";

        /// <summary>
        /// The billing and usage report category.
        /// </summary>
        public const string BillingUsage = "Billing & Usage";

        /// <summary>
        /// The commerce report category.
        /// </summary>
        public const string Commerce = "Commerce";

        /// <summary>
        /// The CRM and campaign report category.
        /// </summary>
        public const string CrmCampaigns = "CRM & Campaigns";

        /// <summary>
        /// The AI report category: chat session analytics, extracted data and conversion goals.
        /// </summary>
        public const string AIReports = "Artificial Intelligence Reports";

        /// <summary>
        /// The compliance and audit report category.
        /// </summary>
        public const string ComplianceAudit = "Compliance & Audit";

        /// <summary>
        /// The technical and IT report category.
        /// </summary>
        public const string Technical = "Technical & IT";

        /// <summary>
        /// The general report category.
        /// </summary>
        public const string General = "General";

        /// <summary>
        /// Gets the preferred display order for a category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <returns>The category display order.</returns>
        public static int GetOrder(string category)
        {
            return category switch
            {
                Executive => 10,
                Operations => 20,
                QueueRouting => 30,
                AgentPerformance => 40,
                WorkforcePayroll => 50,
                BillingUsage => 60,
                Commerce => 65,
                CrmCampaigns => 70,
                AIReports => 75,
                ComplianceAudit => 80,
                Technical => 90,
                _ => 100,
            };
        }
    }
}
