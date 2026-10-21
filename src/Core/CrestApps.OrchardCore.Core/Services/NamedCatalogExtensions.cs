using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Provides the checks a named catalog entry's handler runs before the entry is stored.
/// </summary>
public static class NamedCatalogExtensions
{
    /// <summary>
    /// Determines whether another entry of the catalog already uses the entry's name.
    /// </summary>
    /// <typeparam name="T">The catalog entry type.</typeparam>
    /// <param name="catalog">The catalog the entry belongs to.</param>
    /// <param name="entry">The entry about to be stored.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns><see langword="true"/> when an entry with another identifier has the same name; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> IsNameUsedByAnotherEntryAsync<T>(
        this INamedCatalog<T> catalog,
        T entry,
        CancellationToken cancellationToken = default)
        where T : CatalogItem, INameAwareModel
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return false;
        }

        var existing = await catalog.FindByNameAsync(entry.Name, cancellationToken);

        return existing is not null && !string.Equals(existing.ItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase);
    }
}
