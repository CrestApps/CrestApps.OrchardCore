using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the entries of a catalog that a recipe step carries in one array, creating the entries that do not exist
/// and updating those that do.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier, and a derived step can match it some other way through
/// <see cref="FindExistingAsync"/>. An update is checked on a detached copy first, so an invalid entry is reported as a
/// recipe error instead of being written to the store. The step reads the same JSON shape the matching
/// <see cref="CatalogDeploymentSource{TStep, TEntry}"/> writes.
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class CatalogRecipeStep<TEntry> : NamedRecipeStepHandler
    where TEntry : CatalogItem
{
    private readonly string _entriesName;

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
        : base(stepName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entriesName);
        ArgumentNullException.ThrowIfNull(manager);

        _entriesName = entriesName;
        Manager = manager;
    }

    /// <summary>
    /// Gets the manager the entries are created and updated through.
    /// </summary>
    protected ICatalogManager<TEntry> Manager { get; }

    /// <summary>
    /// Gets whether an entry can be imported at all, before it is matched. A step that refuses an entry adds the reason
    /// to the recipe errors.
    /// </summary>
    /// <param name="data">The JSON of the entry.</param>
    /// <param name="context">The recipe step being run.</param>
    /// <returns><see langword="true"/> to import the entry; otherwise, <see langword="false"/>.</returns>
    protected virtual bool CanImport(JsonObject data, RecipeExecutionContext context)
        => true;

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
    /// Gives a derived step the chance to fold a new entry into one that is already stored instead of creating it.
    /// </summary>
    /// <param name="entry">The new, unsaved entry.</param>
    /// <returns><see langword="true"/> when the entry was handled and must not be created; otherwise, <see langword="false"/>.</returns>
    protected virtual ValueTask<bool> TryMergeNewEntryAsync(TEntry entry)
        => ValueTask.FromResult(false);

    /// <summary>
    /// Applies the JSON of an entry onto a detached copy of the stored entry, the same way the entry's handler applies
    /// it when the update is saved.
    /// </summary>
    /// <param name="entry">The copy to populate.</param>
    /// <param name="data">The JSON of the entry.</param>
    protected virtual void Populate(TEntry entry, JsonObject data)
        => CatalogDeploymentSerializer.Populate(entry, data);

    /// <summary>
    /// Creates a detached copy of a stored entry, so an update can be checked without changing the stored entry.
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <returns>A copy of the entry.</returns>
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
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in CatalogRecipeSections.Section(context.Step, _entriesName))
        {
            if (!CanImport(token, context))
            {
                continue;
            }

            var id = token[nameof(CatalogItem.ItemId)] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var hasId = !string.IsNullOrEmpty(id);

            var entry = await FindExistingAsync(id, token);
            var isNew = entry is null;
            TEntry candidate;

            if (isNew)
            {
                entry = await NewEntryAsync(token);

                if (entry is null)
                {
                    continue;
                }

                if (hasId && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }

                if (await TryMergeNewEntryAsync(entry))
                {
                    continue;
                }

                candidate = entry;
            }
            else
            {
                candidate = CreateDetachedCopy(entry);
                Populate(candidate, token);
            }

            var validationResult = await Manager.ValidateAsync(candidate);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await Manager.CreateAsync(entry);
            }
            else
            {
                await Manager.UpdateAsync(entry, token);
            }
        }
    }
}
