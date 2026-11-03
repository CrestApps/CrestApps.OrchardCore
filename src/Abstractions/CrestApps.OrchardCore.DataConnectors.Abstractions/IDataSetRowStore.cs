using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Stores the rows of imported data sets in the tenant's database, by version.
/// </summary>
/// <remarks>
/// A refresh writes a new version beside the one consumers read and publishes it by changing the data set's active
/// version, so a refresh that fails leaves the published rows as they were. A row is a JSON object keyed by the
/// data set's field names.
/// </remarks>
public interface IDataSetRowStore
{
    /// <summary>
    /// Adds rows to a version of a data set.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="version">The version written.</param>
    /// <param name="rows">The rows, each with its identity key or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    /// <returns>A task that completes when the rows are committed.</returns>
    Task WriteAsync(string dataSetId, string version, IReadOnlyList<DataSetRow> rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies every row of a version into another version of the same data set.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="fromVersion">The version copied.</param>
    /// <param name="toVersion">The version written.</param>
    /// <param name="cancellationToken">The token that cancels the copy.</param>
    /// <returns>The number of rows copied.</returns>
    Task<long> CopyAsync(string dataSetId, string fromVersion, string toVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the rows of a version that have any of the keys.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="version">The version changed.</param>
    /// <param name="keys">The row keys.</param>
    /// <param name="cancellationToken">The token that cancels the delete.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<long> DeleteKeysAsync(string dataSetId, string version, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of the rows of a version, in the order they were written.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="version">The version read.</param>
    /// <param name="afterId">The position to read after, or zero for the first page.</param>
    /// <param name="take">The most rows to read.</param>
    /// <param name="keys">Only the rows with these keys, or <see langword="null"/> for every row.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The rows, with their positions.</returns>
    Task<IReadOnlyList<StoredDataSetRow>> ReadAsync(
        string dataSetId,
        string version,
        long afterId,
        int take,
        IReadOnlyCollection<string> keys = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the rows of a version.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="version">The version counted.</param>
    /// <param name="cancellationToken">The token that cancels the count.</param>
    /// <returns>The number of rows.</returns>
    Task<long> CountAsync(string dataSetId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every row of a version.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="version">The version deleted.</param>
    /// <param name="cancellationToken">The token that cancels the delete.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    Task DeleteVersionAsync(string dataSetId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every row of a data set but those of the versions kept.
    /// </summary>
    /// <param name="dataSetId">The data set's identifier.</param>
    /// <param name="keepVersions">The versions kept, or <see langword="null"/> to delete every row.</param>
    /// <param name="cancellationToken">The token that cancels the delete.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    Task DeleteAllAsync(string dataSetId, IReadOnlyCollection<string> keepVersions = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// A row written to an imported data set.
/// </summary>
/// <param name="Key">The row's identity key, or <see langword="null"/> when the data set has no identifier.</param>
/// <param name="Data">The values, keyed by data set field name.</param>
public sealed record DataSetRow(string Key, JsonObject Data);

/// <summary>
/// A row read from an imported data set.
/// </summary>
/// <param name="Id">The row's position, to read after.</param>
/// <param name="Key">The row's identity key, or <see langword="null"/>.</param>
/// <param name="Data">The values, keyed by data set field name.</param>
public sealed record StoredDataSetRow(long Id, string Key, JsonObject Data);
