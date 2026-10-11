using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// A connector the report builder reads data from. A data source exposes one or more data sets (for example the
/// content types of the tenant, the tables of a database, or the indexes of a search server), describes the typed
/// fields of each one, and returns their rows. Register an implementation as a scoped service to make its data sets
/// available in the report builder.
/// </summary>
public interface IReportDataSource
{
    /// <summary>
    /// Gets the stable technical name of the data source. Report designs store this name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the label shown in the designer.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the description shown in the designer.
    /// </summary>
    LocalizedString Description { get; }

    /// <summary>
    /// Lists the data sets the principal in <paramref name="context"/> is allowed to read.
    /// </summary>
    /// <param name="context">The caller context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The accessible data sets.</returns>
    Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes the fields of a data set.
    /// </summary>
    /// <param name="dataSet">The technical name of the data set.</param>
    /// <param name="context">The caller context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The schema, or <see langword="null"/> when the data set does not exist or the principal in
    /// <paramref name="context"/> is not allowed to read it. The report engine reads a data set only after this method
    /// returns its schema.
    /// </returns>
    Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the rows of a data set.
    /// </summary>
    /// <param name="query">The data set, fields, optional conditions, and row limit to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rows read.</returns>
    Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default);
}
