using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Records, on the context of a read, that a data set returned only the rows its reader may see. A stored result read
/// with such a context belongs to the reader it was read for: a scheduled report view keeps it for its owner and runs
/// live for everyone else. A custom <see cref="IDataSource"/> that filters rows by <see cref="DataSourceContext.User"/>
/// must call <see cref="MarkRestricted(DataSourceContext)"/>; <c>IndexedReportDataSet</c> does it for every data set that
/// overrides its <c>RestrictAsync</c>.
/// </summary>
public static class ReportRowScope
{
    private const string PropertyName = "CrestApps.Reports.RowsRestrictedByReader";

    /// <summary>
    /// Records that the rows read with the context depend on its reader.
    /// </summary>
    /// <param name="context">The context of the read.</param>
    public static void MarkRestricted(DataSourceContext context)
    {
        if (context is not null)
        {
            context.Properties[PropertyName] = true;
        }
    }

    /// <summary>
    /// Gets whether any data set read with the context returned only the rows its reader may see.
    /// </summary>
    /// <param name="context">The context of the read.</param>
    public static bool IsRestricted(DataSourceContext context)
        => context is not null && context.Properties.TryGetValue(PropertyName, out var value) && value is true;
}
