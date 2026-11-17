using System.Security.Claims;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.AI.Agent.DataPipelines;

/// <summary>
/// Lists the data sets a user may read, the way the pipeline designer's Add a data source dialog does
/// (<c>DataSetOption.ListAsync</c> in the Data Pipelines module, which a module cannot call): every data set of every
/// registered data source, asked with the user, so each data source leaves out what the user may not read.
/// </summary>
internal static class DataSetCatalog
{
    /// <summary>
    /// The separator of the data source and the data set in the value a "Read a data source" step uses.
    /// </summary>
    public const string Separator = "::";

    /// <summary>
    /// Lists the data sets a user may read, in the order of the data sources.
    /// </summary>
    /// <param name="dataSourceManager">The data sources.</param>
    /// <param name="localizerFactory">Localizes the names of the data sources.</param>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data sets.</returns>
    public static async Task<IReadOnlyList<DataSetEntry>> ListAsync(
        IDataSourceManager dataSourceManager,
        IStringLocalizerFactory localizerFactory,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSourceManager);
        ArgumentNullException.ThrowIfNull(localizerFactory);

        var entries = new List<DataSetEntry>();
        var context = new DataSourceContext { User = user };

        foreach (var descriptor in dataSourceManager.GetDataSources())
        {
            var dataSource = dataSourceManager.GetDataSource(descriptor.Name);

            if (dataSource is null)
            {
                continue;
            }

            var sourceName = localizerFactory.Localize(descriptor.DisplayName)?.Value ?? descriptor.Name;

            foreach (var dataSet in await dataSource.GetDataSetsAsync(context, cancellationToken))
            {
                entries.Add(new DataSetEntry(descriptor.Name, sourceName, dataSet));
            }
        }

        return entries;
    }

    /// <summary>
    /// Splits the value of a data set into its data source and data set.
    /// </summary>
    /// <param name="value">The value, such as <c>Contents::BlogPost</c>.</param>
    /// <returns>The data source and the data set, both <see langword="null"/> when the value has no separator.</returns>
    public static (string Source, string DataSet) Parse(string value)
    {
        var parts = (value ?? string.Empty).Trim().Split(Separator, 2);

        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[0], parts[1])
            : (null, null);
    }

    /// <summary>
    /// A data set with the data source that provides it.
    /// </summary>
    /// <param name="Source">The technical name of the data source.</param>
    /// <param name="SourceName">The localized name of the data source.</param>
    /// <param name="DataSet">The data set.</param>
    public sealed record DataSetEntry(string Source, string SourceName, DataSetDescriptor DataSet)
    {
        /// <summary>
        /// Gets the value a "Read a data source" step uses for the data set, such as <c>Contents::BlogPost</c>.
        /// </summary>
        public string Value => Source + Separator + DataSet.Name;
    }
}
