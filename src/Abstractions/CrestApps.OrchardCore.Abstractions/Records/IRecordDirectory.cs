using System.Security.Claims;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// Reads records of every kind through the registered <see cref="IRecordSource"/>s.
/// </summary>
public interface IRecordDirectory
{
    /// <summary>
    /// Gets the sources, one per kind, in registration order.
    /// </summary>
    IReadOnlyList<IRecordSource> Sources { get; }

    /// <summary>
    /// Gets the source of a kind, or <see langword="null"/> when nothing registers one.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The source.</returns>
    IRecordSource GetSource(string kind);

    /// <summary>
    /// Gets the sources whose records a user may search.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the checks.</param>
    /// <returns>The sources.</returns>
    Task<IReadOnlyList<IRecordSource>> GetSearchableSourcesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds records of a kind the searching user may see.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="search">The text, the count and the user.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The records found, or none when the kind has no source or the user may not search it.</returns>
    Task<IReadOnlyList<RecordSummary>> SearchAsync(string kind, RecordSearch search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes one record.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The summary, or <see langword="null"/> when it isn't found or its kind has no source.</returns>
    Task<RecordSummary> GetSummaryAsync(RecordRef record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes several records, with one lookup per kind.
    /// </summary>
    /// <param name="records">The records.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The summaries of the records found.</returns>
    Task<IReadOnlyDictionary<RecordRef, RecordSummary>> GetSummariesAsync(IEnumerable<RecordRef> records, CancellationToken cancellationToken = default);
}
