using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the catalog entries a recipe step carries in one array: an entry that is found is updated, any other entry
/// is created, and every entry is validated before it is created.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier first, then by <see cref="FindExistingAsync"/>, which a step overrides to
/// match by name, or by name and source. A found entry is updated through its manager the way the manager applies
/// JSON, then validated; an entry that fails validation is reported as a recipe error. Use
/// <see cref="NamedCatalogRecipeStep{TEntry}"/> instead when updates must be checked on a detached copy before they are
/// written.
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class CatalogRecipeStepBase<TEntry> : NamedRecipeStepHandler
    where TEntry : CatalogItem
{
    private readonly string _entriesName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRecipeStepBase{TEntry}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of entries, matched ignoring case.</param>
    protected CatalogRecipeStepBase(string stepName, string entriesName)
        : base(stepName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entriesName);

        _entriesName = entriesName;
    }

    /// <summary>
    /// Gets whether a found entry is validated after it is updated. A step whose manager validates on update turns
    /// this off.
    /// </summary>
    protected virtual bool ValidateUpdatedEntries => true;

    /// <summary>
    /// Says whether an entry can be imported at all, before it is looked up. Add an error to the context and return
    /// <see langword="false"/> to skip it.
    /// </summary>
    /// <param name="token">The JSON of the entry.</param>
    /// <param name="context">The recipe execution context.</param>
    /// <returns><see langword="true"/> when the entry is imported.</returns>
    protected virtual bool CanImport(JsonObject token, RecipeExecutionContext context)
        => true;

    /// <summary>
    /// Finds the stored entry with the given identifier.
    /// </summary>
    /// <param name="id">The identifier the recipe carries.</param>
    /// <returns>The stored entry, or <see langword="null"/>.</returns>
    protected abstract ValueTask<TEntry> FindByIdAsync(string id);

    /// <summary>
    /// Finds the stored entry an entry without a known identifier stands for, by name or by name and source. Nothing is
    /// matched by default. Add an error to the context and return <see cref="CatalogRecipeLookup{TEntry}.Skip"/> to
    /// skip the entry.
    /// </summary>
    /// <param name="token">The JSON of the entry.</param>
    /// <param name="context">The recipe execution context.</param>
    /// <returns>The lookup result.</returns>
    protected virtual ValueTask<CatalogRecipeLookup<TEntry>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
        => ValueTask.FromResult(CatalogRecipeLookup<TEntry>.NotFound);

    /// <summary>
    /// Applies the JSON of an entry onto the stored entry and saves it.
    /// </summary>
    /// <param name="entry">The stored entry.</param>
    /// <param name="token">The JSON of the entry.</param>
    /// <returns>A task that completes when the entry is updated.</returns>
    protected abstract ValueTask UpdateAsync(TEntry entry, JsonObject token);

    /// <summary>
    /// Creates a new, unsaved entry from its JSON. Add an error to the context and return <see langword="null"/> when
    /// the entry cannot be created, for example because its source is missing or unknown.
    /// </summary>
    /// <param name="token">The JSON of the entry.</param>
    /// <param name="context">The recipe execution context.</param>
    /// <returns>The new entry, or <see langword="null"/> to skip it.</returns>
    protected abstract ValueTask<TEntry> NewAsync(JsonObject token, RecipeExecutionContext context);

    /// <summary>
    /// Completes an entry right before it is validated, for both new and updated entries.
    /// </summary>
    /// <param name="entry">The entry about to be validated.</param>
    /// <param name="token">The JSON of the entry.</param>
    protected virtual void Preparing(TEntry entry, JsonObject token)
    {
    }

    /// <summary>
    /// Validates an entry.
    /// </summary>
    /// <param name="entry">The entry to validate.</param>
    /// <returns>The validation result.</returns>
    protected abstract ValueTask<ValidationResultDetails> ValidateAsync(TEntry entry);

    /// <summary>
    /// Saves a new entry.
    /// </summary>
    /// <param name="entry">The new entry.</param>
    /// <returns>A task that completes when the entry is saved.</returns>
    protected abstract ValueTask CreateAsync(TEntry entry);

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in GetEntries(context.Step))
        {
            if (!CanImport(token, context))
            {
                continue;
            }

            var id = token[nameof(CatalogItem.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            var entry = hasId ? await FindByIdAsync(id) : null;

            if (entry is null)
            {
                var lookup = await FindExistingAsync(token, context);

                if (lookup.IsSkipped)
                {
                    continue;
                }

                entry = lookup.Entry;
            }

            var isNew = entry is null;

            if (isNew)
            {
                entry = await NewAsync(token, context);

                if (entry is null)
                {
                    continue;
                }

                if (hasId && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }
            }
            else
            {
                await UpdateAsync(entry, token);

                if (!ValidateUpdatedEntries)
                {
                    continue;
                }
            }

            Preparing(entry, token);

            var validationResult = await ValidateAsync(entry);

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
                await CreateAsync(entry);
            }
        }
    }

    private IEnumerable<JsonObject> GetEntries(JsonObject step)
    {
        if (step is null)
        {
            return [];
        }

        if (!step.TryGetPropertyValue(_entriesName, out var entries))
        {
            entries = step.FirstOrDefault(property => string.Equals(property.Key, _entriesName, StringComparison.OrdinalIgnoreCase)).Value;
        }

        return (entries as JsonArray ?? []).OfType<JsonObject>();
    }
}
