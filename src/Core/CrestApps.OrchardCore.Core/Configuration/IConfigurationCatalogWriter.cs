using System.Text.Json.Nodes;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// The operations a configuration catalog needs from the manager that owns its entries.
/// </summary>
/// <remarks>
/// A catalog entry is owned either by a plain manager or by a source-backed one, and the two describe the same
/// operations through separate interfaces that share no write-capable ancestor. Importing and exporting configuration
/// does not care which of the two an entry happens to use, so the catalog depends on this shape and an adapter carries
/// each manager into it. Without it the catalog would have to be written twice, and the second copy is the one that
/// would fall behind.
/// </remarks>
/// <typeparam name="T">The catalog entry type.</typeparam>
public interface IConfigurationCatalogWriter<T>
    where T : CatalogItem
{
    /// <summary>
    /// Gets every entry the catalog holds.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>Every entry currently stored.</returns>
    ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds the entry that carries the given identifier.
    /// </summary>
    /// <param name="itemId">The identifier of the entry to find.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The entry, or <see langword="null"/> when the catalog holds no entry with that identifier.</returns>
    ValueTask<T> FindByIdAsync(string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// Builds a new entry from the given data without storing it.
    /// </summary>
    /// <param name="data">The data the recipe or deployment plan carries for the entry.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new entry.</returns>
    ValueTask<T> NewAsync(JsonNode data, CancellationToken cancellationToken);

    /// <summary>
    /// Validates an entry through the rules its handlers declare.
    /// </summary>
    /// <param name="entry">The entry to validate.</param>
    /// <param name="cancellationToken">A token that cancels the validation.</param>
    /// <returns>The result of every rule that ran.</returns>
    ValueTask<ValidationResultDetails> ValidateAsync(T entry, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    ValueTask CreateAsync(T entry, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an entry that the catalog already holds.
    /// </summary>
    /// <param name="entry">The entry to update.</param>
    /// <param name="data">The data the recipe or deployment plan carries for the entry.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the entry is updated.</returns>
    ValueTask UpdateAsync(T entry, JsonNode data, CancellationToken cancellationToken);
}
