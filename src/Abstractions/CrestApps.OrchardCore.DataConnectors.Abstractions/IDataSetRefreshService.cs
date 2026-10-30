using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Refreshes imported data sets: reads the source, stores a new version of the rows, and publishes it when every row
/// was written.
/// </summary>
public interface IDataSetRefreshService
{
    /// <summary>
    /// Refreshes an imported data set now, unless another refresh of it is running.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="trigger">What started the refresh.</param>
    /// <param name="requestedBy">The user name of whoever asked for it, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token that cancels the refresh.</param>
    /// <returns>The refresh, as it ended.</returns>
    Task<DataSetRefreshRun> RefreshAsync(
        ConnectedDataSet dataSet,
        DataSetRefreshTrigger trigger,
        string requestedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the imported data sets that are due or were asked for, one after the other.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the refreshes.</param>
    /// <returns>The number of refreshes run.</returns>
    Task<int> RefreshDueAsync(CancellationToken cancellationToken = default);
}
