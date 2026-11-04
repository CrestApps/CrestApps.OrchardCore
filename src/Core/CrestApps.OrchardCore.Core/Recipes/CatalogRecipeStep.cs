using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the entries of a catalog that a recipe step carries in one array, creating the entries that do not exist
/// and updating those that do.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier, and a derived step can match it some other way through
/// <see cref="FindExistingAsync(string, JsonObject)"/>. An update is checked on a detached copy first, so an invalid entry is reported as a
/// recipe error instead of being written to the store. The step reads the same JSON shape the matching
/// <see cref="CatalogDeploymentSource{TStep, TEntry}"/> writes.
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class CatalogRecipeStep<TEntry> : CatalogRecipeStepBase<TEntry>
    where TEntry : CatalogItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRecipeStep{TEntry}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    /// <param name="manager">The manager the entries are created and updated through.</param>
    protected CatalogRecipeStep(
        string stepName,
        string entriesName,
        ICatalogManager<TEntry> manager)
        : base(stepName, entriesName)
    {
        ArgumentNullException.ThrowIfNull(manager);

        Manager = manager;
    }

    /// <summary>
    /// Gets the manager the entries are created and updated through.
    /// </summary>
    protected ICatalogManager<TEntry> Manager { get; }

    /// <summary>
    /// Finds the stored entry the JSON describes, by its identifier.
    /// </summary>
    /// <param name="id">The identifier the JSON carries, if any.</param>
    /// <param name="data">The JSON of the entry.</param>
    /// <returns>The stored entry, or <see langword="null"/> when the entry is new.</returns>
    protected virtual async ValueTask<TEntry> FindExistingAsync(string id, JsonObject data)
        => string.IsNullOrEmpty(id) ? null : await Manager.FindByIdAsync(id);

    /// <summary>
    /// Creates a new entry from its JSON.
    /// </summary>
    /// <param name="data">The JSON of the entry.</param>
    /// <returns>The new, unsaved entry.</returns>
    protected virtual ValueTask<TEntry> NewEntryAsync(JsonObject data)
        => Manager.NewAsync(data);

    /// <summary>
    /// Applies the JSON of an entry onto a detached copy of the stored entry, the same way the entry's handler applies
    /// it when the update is saved.
    /// </summary>
    /// <param name="entry">The detached copy.</param>
    /// <param name="data">The JSON of the entry.</param>
    protected virtual void Populate(TEntry entry, JsonObject data)
        => CatalogDeploymentSerializer.Populate(entry, data);

    /// <summary>
    /// Creates a detached copy of a stored entry, so an update can be checked without changing the stored entry.
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <returns>The copy.</returns>
    protected virtual TEntry CreateDetachedCopy(TEntry entry)
    {
        if (entry is ICloneable<TEntry> cloneable)
        {
            return cloneable.Clone();
        }

        var node = JsonSerializer.SerializeToNode(entry, entry.GetType(), CatalogDeploymentSerializer.Options);

        return (TEntry)node.Deserialize(entry.GetType(), CatalogDeploymentSerializer.Options);
    }

    /// <inheritdoc/>
    /// <remarks>Every entry is matched through <see cref="FindExistingAsync(string, JsonObject)"/>.</remarks>
    protected sealed override ValueTask<TEntry> FindByIdAsync(string id)
        => ValueTask.FromResult<TEntry>(null);

    /// <inheritdoc/>
    protected sealed override async ValueTask<CatalogRecipeLookup<TEntry>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var id = token[nameof(CatalogItem.ItemId)] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var entry = await FindExistingAsync(id, token);

        return entry is null ? CatalogRecipeLookup<TEntry>.NotFound : CatalogRecipeLookup<TEntry>.Found(entry);
    }

    /// <inheritdoc/>
    protected sealed override ValueTask<TEntry> NewAsync(JsonObject token, RecipeExecutionContext context)
        => NewEntryAsync(token);

    /// <inheritdoc/>
    /// <remarks>The JSON is applied onto a detached copy, which is validated; the stored entry is not changed yet.</remarks>
    protected sealed override ValueTask<TEntry> PrepareUpdateAsync(TEntry entry, JsonObject token)
    {
        var candidate = CreateDetachedCopy(entry);
        Populate(candidate, token);

        return ValueTask.FromResult(candidate);
    }

    /// <inheritdoc/>
    protected sealed override async ValueTask CommitUpdateAsync(TEntry entry, JsonObject token)
        => await Manager.UpdateAsync(entry, token);

    /// <inheritdoc/>
    protected override async ValueTask<ValidationResultDetails> ValidateAsync(TEntry entry)
        => await Manager.ValidateAsync(entry);

    /// <inheritdoc/>
    protected override async ValueTask CreateAsync(TEntry entry)
        => await Manager.CreateAsync(entry);
}
