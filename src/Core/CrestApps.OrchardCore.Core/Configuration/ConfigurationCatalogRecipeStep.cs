using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Imports every registered <see cref="IConfigurationCatalog"/> by matching the executing recipe step to the
/// catalog that declares it, so a catalog is scriptable as soon as it is registered rather than only once
/// somebody remembers to write a step handler for it.
/// </summary>
public sealed class ConfigurationCatalogRecipeStep : IRecipeStepHandler
{
    private readonly IEnumerable<IConfigurationCatalog> _catalogs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCatalogRecipeStep"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    public ConfigurationCatalogRecipeStep(IEnumerable<IConfigurationCatalog> catalogs)
    {
        _catalogs = catalogs;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(RecipeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var matched = false;
        var imported = false;

        foreach (var catalog in _catalogs)
        {
            if (!string.Equals(catalog.StepName, context.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matched = true;

            if (!TryGetCollection(context.Step, catalog.CollectionName, out var entries))
            {
                continue;
            }

            if (entries is null)
            {
                context.Errors.Add($"The '{context.Name}' step declares '{catalog.CollectionName}', but it is not a list of entries, so none of that configuration was imported.");

                continue;
            }

            imported = true;

            await catalog.ImportAsync(entries, context);
        }

        if (!matched)
        {
            return;
        }

        if (!imported)
        {
            // A step that names nothing this tenant recognises imported nothing, and reporting that as a success
            // hands the operator a tenant that looks configured because the plan ran without complaint.
            context.Errors.Add($"The '{context.Name}' step carries none of the configuration it can import. Expected at least one of: {string.Join(", ", CollectionNames(context.Name))}.");

            return;
        }

        // Configuration is a graph, and a plan is a sequence. A queue references a channel endpoint that a different
        // deployment step exports, and it overflows into a queue in its own step, so no ordering of the plan makes
        // every reference resolvable at the moment it is written. Every catalog is therefore offered the chance to
        // repair what it has already stored after each step, which makes the import independent of the order the
        // steps happen to appear in.
        foreach (var catalog in _catalogs)
        {
            await catalog.RepairReferencesAsync(context);
        }
    }

    private IEnumerable<string> CollectionNames(string stepName)
    {
        foreach (var catalog in _catalogs)
        {
            if (string.Equals(catalog.StepName, stepName, StringComparison.OrdinalIgnoreCase))
            {
                yield return catalog.CollectionName;
            }
        }
    }

    private static bool TryGetCollection(JsonObject step, string collectionName, out JsonArray entries)
    {
        entries = null;

        if (step is null)
        {
            return false;
        }

        // A plan is a file an operator can write, and the recipe engine binds its own steps without regard to case,
        // so a collection spelled the way it reads has to be recognised rather than quietly skipped.
        foreach (var property in step)
        {
            if (!string.Equals(property.Key, collectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is null || property.Value.GetValueKind() == JsonValueKind.Null)
            {
                return false;
            }

            entries = property.Value as JsonArray;

            return true;
        }

        return false;
    }
}
