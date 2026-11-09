using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// The result of looking up the stored entry a recipe entry stands for in a <see cref="CatalogRecipeStepBase{TEntry}"/>.
/// </summary>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public readonly record struct CatalogRecipeLookup<TEntry>
    where TEntry : CatalogItem
{
    /// <summary>
    /// Gets a result that matched no stored entry, so the entry is created.
    /// </summary>
    public static CatalogRecipeLookup<TEntry> NotFound => default;

    /// <summary>
    /// Gets a result that skips the entry; the step has already reported why.
    /// </summary>
    public static CatalogRecipeLookup<TEntry> Skip => new() { IsSkipped = true };

    /// <summary>
    /// Gets the stored entry that was found, or <see langword="null"/>.
    /// </summary>
    public TEntry Entry { get; init; }

    /// <summary>
    /// Gets whether the entry is skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Creates a result for a stored entry, which is <see langword="null"/> when nothing matched.
    /// </summary>
    /// <param name="entry">The stored entry, or <see langword="null"/>.</param>
    /// <returns>The lookup result.</returns>
    public static CatalogRecipeLookup<TEntry> Found(TEntry entry) => new() { Entry = entry };
}
