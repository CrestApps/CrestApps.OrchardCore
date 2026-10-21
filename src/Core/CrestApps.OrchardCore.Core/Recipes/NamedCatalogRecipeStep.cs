using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the entries of a named catalog that a recipe step carries in one array, creating the entries that do not
/// exist and updating those that do.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier, and by its name too when <see cref="MatchByName"/> says so. An update is
/// checked on a detached copy first, so an invalid entry is reported as a recipe error instead of being written to
/// the store. The step reads the same JSON shape the matching <see cref="NamedCatalogDeploymentSource{TStep, TEntry}"/>
/// writes.
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class NamedCatalogRecipeStep<TEntry> : NamedRecipeStepHandler
    where TEntry : CatalogItem, INameAwareModel, ICloneable<TEntry>, new()
{
    private readonly string _entriesName;
    private readonly INamedCatalogManager<TEntry> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedCatalogRecipeStep{TEntry}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    /// <param name="manager">The manager the entries are created and updated through.</param>
    protected NamedCatalogRecipeStep(
        string stepName,
        string entriesName,
        INamedCatalogManager<TEntry> manager)
        : base(stepName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entriesName);

        _entriesName = entriesName;
        _manager = manager;
    }

    /// <summary>
    /// Gets whether an entry without a known identifier is matched to a stored entry of the same name.
    /// </summary>
    protected virtual bool MatchByName => false;

    /// <summary>
    /// Applies the JSON of an entry onto a detached copy of the stored entry, the same way the entry's handler applies
    /// it when the update is saved.
    /// </summary>
    /// <param name="entry">The copy to populate.</param>
    /// <param name="data">The JSON of the entry.</param>
    protected virtual void Populate(TEntry entry, JsonObject data)
        => CatalogDeploymentSerializer.Populate(entry, data);

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in GetEntries(context.Step))
        {
            var id = token[nameof(CatalogItem.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            var entry = hasId ? await _manager.FindByIdAsync(id) : null;

            if (entry is null && MatchByName)
            {
                var name = token[nameof(INameAwareModel.Name)]?.GetValue<string>()?.Trim();

                entry = !string.IsNullOrEmpty(name) ? await _manager.FindByNameAsync(name) : null;
            }

            var isNew = entry is null;
            TEntry candidate;

            if (isNew)
            {
                entry = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }

                candidate = entry;
            }
            else
            {
                candidate = entry.Clone();
                Populate(candidate, token);
            }

            var validationResult = await _manager.ValidateAsync(candidate);

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
                await _manager.CreateAsync(entry);
            }
            else
            {
                await _manager.UpdateAsync(entry, token);
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
