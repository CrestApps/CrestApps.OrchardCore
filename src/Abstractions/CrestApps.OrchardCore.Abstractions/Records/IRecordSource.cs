using System.Security.Claims;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Records;

/// <summary>
/// Finds and describes the records of one kind, such as orders or assets. The module that keeps the records registers
/// one with <see cref="RecordsServiceCollectionExtensions.AddRecordSource{TSource}"/>, so any module can link to them,
/// pick them and show their titles without referencing that module.
/// </summary>
public interface IRecordSource
{
    /// <summary>
    /// Gets the kind of record this source finds. It is stored in every <see cref="RecordRef"/>, so it never changes.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the name of the kind shown to users, such as "Order".
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets whether a user may search and pick records of this kind at all.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns><see langword="true"/> when the user may.</returns>
    Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds records the searching user may see.
    /// </summary>
    /// <param name="search">The text, the count and the user.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The records found.</returns>
    Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes records by their ids. Ids that aren't found are left out.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The summaries by id.</returns>
    Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
}
