using CrestApps.Core.Services;
using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Stores the tenant's data connections.
/// </summary>
public interface IDataConnectionStore : ICatalog<DataConnection>
{
    /// <summary>
    /// Finds a connection by its technical name.
    /// </summary>
    /// <param name="name">The name, ignoring case.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The connection, or <see langword="null"/>.</returns>
    Task<DataConnection> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every connection, by display name.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The connections.</returns>
    Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores the tenant's connected data sets.
/// </summary>
public interface IConnectedDataSetStore : ICatalog<ConnectedDataSet>
{
    /// <summary>
    /// Finds a data set by its technical name.
    /// </summary>
    /// <param name="name">The name, ignoring case.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The data set, or <see langword="null"/>.</returns>
    Task<ConnectedDataSet> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every data set, by display name.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The data sets.</returns>
    Task<IReadOnlyList<ConnectedDataSet>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the data sets that read a connection.
    /// </summary>
    /// <param name="connectionId">The connection's identifier.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The data sets.</returns>
    Task<IReadOnlyList<ConnectedDataSet>> ListByConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the enabled imported data sets whose refresh is due or was asked for.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The data sets, the most overdue first.</returns>
    Task<IReadOnlyList<ConnectedDataSet>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores the refreshes of imported data sets.
/// </summary>
public interface IDataSetRefreshRunStore : ICatalog<DataSetRefreshRun>
{
    /// <summary>
    /// Returns a page of a data set's refreshes, the newest first, and how many there are.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="skip">The number of refreshes skipped.</param>
    /// <param name="take">The most refreshes returned.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<DataSetRefreshRun> Runs)> PageAsync(string dataSetId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a data set's refreshes but the newest ones.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="keep">The number of newest refreshes kept.</param>
    /// <param name="cancellationToken">The token that cancels the delete.</param>
    /// <returns>The number of refreshes deleted.</returns>
    Task<int> PruneAsync(string dataSetId, int keep, CancellationToken cancellationToken = default);
}
