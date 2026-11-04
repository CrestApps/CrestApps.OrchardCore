using CrestApps.OrchardCore.Assets.Models;

namespace CrestApps.OrchardCore.Assets.Services;

/// <summary>
/// Finds and names the customers, companies or contacts of one kind that can own an asset. A feature that keeps
/// such records registers one, so the asset editor can pick them instead of typing their numbers.
/// </summary>
public interface IAssetOwnerSource
{
    /// <summary>
    /// Gets the kind of owner this source finds.
    /// </summary>
    AssetOwnerKind Kind { get; }

    /// <summary>
    /// Finds owners whose name starts with or contains the text.
    /// </summary>
    /// <param name="query">The text typed, or empty for the first ones.</param>
    /// <param name="count">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The owners found, by name.</returns>
    Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of owners by their ids. Ids that aren't found are left out.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names by id.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// An owner an asset can have.
/// </summary>
/// <param name="Id">The owner's id.</param>
/// <param name="Name">The owner's name.</param>
public sealed record AssetOwnerOption(string Id, string Name);
