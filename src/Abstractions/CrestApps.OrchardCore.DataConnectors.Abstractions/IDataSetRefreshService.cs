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
    /// Refreshes the imported data sets that are due or were asked for, one after the other. A refresh that fails is
    /// recorded and the next one runs; a refresh that can't even record how it ended is logged and stops the run
    /// without throwing, and the data sets still due are refreshed by the next run.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the refreshes.</param>
    /// <returns>The number of refreshes run, succeeded or failed; a refresh skipped because another refresh of the data
    /// set was running isn't counted.</returns>
    Task<int> RefreshDueAsync(CancellationToken cancellationToken = default);
}
