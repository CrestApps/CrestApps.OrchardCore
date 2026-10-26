using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Creates the references a field declares to the data sets of Orchard Core's own data sources, so the field can be
/// joined to the users and content items its values identify.
/// </summary>
public static class ReportDataReferences
{
    /// <summary>
    /// Creates a reference to the identifier of a user of the site.
    /// </summary>
    /// <returns>The reference.</returns>
    public static DataFieldReference User()
    {
        return new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField);
    }

    /// <summary>
    /// Creates a reference to the identifier of a content item of a content type, such as a product.
    /// </summary>
    /// <param name="contentType">The technical name of the content type.</param>
    /// <returns>The reference.</returns>
    public static DataFieldReference ContentItem(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        return new DataFieldReference(ReportsConstants.ContentsDataSource, contentType, ReportsConstants.ContentItemIdField);
    }
}
