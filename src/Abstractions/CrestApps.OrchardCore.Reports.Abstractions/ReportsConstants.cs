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
    /// The technical name of the built-in CSV export format.
    /// </summary>
    public const string CsvExportFormat = "csv";

    /// <summary>
    /// The technical name of the Open XML Excel workbook export format.
    /// </summary>
    public const string XlsxExportFormat = "xlsx";

    /// <summary>
    /// The identifier of the optional Open XML export feature for reports.
    /// </summary>
    public const string OpenXmlFeature = "CrestApps.OrchardCore.Reports.OpenXml";

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
        /// The CRM and campaign report category.
        /// </summary>
        public const string CrmCampaigns = "CRM & Campaigns";

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
                CrmCampaigns => 70,
                ComplianceAudit => 80,
                Technical => 90,
                _ => 100,
            };
        }
    }
}
