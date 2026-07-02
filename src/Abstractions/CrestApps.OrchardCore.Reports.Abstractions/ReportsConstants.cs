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
    /// Contains the well-known report category names used to group reports in the admin navigation.
    /// </summary>
    public static class Categories
    {
        /// <summary>
        /// The customer relationship management report category.
        /// </summary>
        public const string Crm = "CRM";

        /// <summary>
        /// The contact center report category.
        /// </summary>
        public const string ContactCenter = "Contact Center";
    }
}
