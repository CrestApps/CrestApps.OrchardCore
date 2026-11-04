using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Imports the entries of a named catalog that a recipe step carries in one array, creating the entries that do not
/// exist and updating those that do.
/// </summary>
/// <remarks>
/// An entry is matched by its identifier, and by its name too when <see cref="MatchByName"/> says so. An update is
/// checked on a detached copy first, so an invalid entry is reported as a recipe error instead of being written to
/// the store. The step reads the same JSON shape the matching
/// <see cref="Deployments.NamedCatalogDeploymentSource{TStep, TEntry}"/> writes.
/// </remarks>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class NamedCatalogRecipeStep<TEntry> : CatalogRecipeStep<TEntry>
    where TEntry : CatalogItem, INameAwareModel, new()
{
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
        : base(stepName, entriesName, manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Gets whether an entry without a known identifier is matched to a stored entry of the same name.
    /// </summary>
    protected virtual bool MatchByName => false;

    /// <inheritdoc/>
    protected override async ValueTask<TEntry> FindExistingAsync(string id, JsonObject data)
    {
        var entry = await base.FindExistingAsync(id, data);

        if (entry is null && MatchByName)
        {
            var name = data[nameof(INameAwareModel.Name)] is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null;

            entry = !string.IsNullOrEmpty(name) ? await _manager.FindByNameAsync(name) : null;
        }

        return entry;
    }
}
