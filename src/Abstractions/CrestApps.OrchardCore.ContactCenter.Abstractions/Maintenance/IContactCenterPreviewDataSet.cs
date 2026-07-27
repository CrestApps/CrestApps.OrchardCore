namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Represents a single persisted Contact Center document type that the preview maintenance tooling can count,
/// export, and reset. Every persisted Contact Center document type must have exactly one registration so that
/// an export can never silently omit a document type and a reset can never silently leave one behind.
/// </summary>
public interface IContactCenterPreviewDataSet
{
    /// <summary>
    /// Gets the stable, machine-readable key identifying this data set. It is the persisted document type name.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the key of the <c>ContactCenterDataGovernanceCatalog</c> category that classifies this data set.
    /// </summary>
    string GovernanceCategoryKey { get; }

    /// <summary>
    /// Gets a value indicating whether this data set holds operator-authored configuration rather than
    /// operational traffic data. Configuration data sets are preserved by an operational-scope reset.
    /// </summary>
    bool IsConfiguration { get; }

    /// <summary>
    /// Counts the documents currently persisted in this data set for the current tenant.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of persisted documents.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of persisted documents in a stable order so an export can stream the whole data set without
    /// materializing it in memory.
    /// </summary>
    /// <param name="skip">The number of documents to skip.</param>
    /// <param name="take">The maximum number of documents to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The documents in the requested page.</returns>
    Task<IReadOnlyList<object>> ReadPageAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every persisted document in this data set for the current tenant.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of documents deleted.</returns>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
}
