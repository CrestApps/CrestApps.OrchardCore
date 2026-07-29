using System.Text.Json.Nodes;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Represents a catalog of tenant configuration that can be exported to a deployment plan and imported from a recipe,
/// so a tenant can be scripted, promoted between environments, and restored from source control.
/// </summary>
public interface IConfigurationCatalog
{
    /// <summary>
    /// Gets the identifier of the group the catalog belongs to, which determines the deployment step that exports it.
    /// </summary>
    string Group { get; }

    /// <summary>
    /// Gets the recipe step name that carries the catalog's entries.
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// Gets the name of the property inside the recipe step that holds the array of entries.
    /// </summary>
    string CollectionName { get; }

    /// <summary>
    /// Gets the relative import order of the catalog, lowest first. A catalog that other catalogs reference must
    /// import before them, because a recipe executes its steps in the order they appear.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Exports every entry in the catalog.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The exported entries, in a form the matching recipe step can import.</returns>
    Task<JsonArray> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the given entries into the catalog, creating entries that do not exist and updating those that do.
    /// </summary>
    /// <param name="entries">The entries to import.</param>
    /// <param name="context">The recipe execution context that collects validation errors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ImportAsync(JsonArray entries, RecipeExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-applies the identifier substitutions made so far in the running import to the entries this catalog has
    /// already imported.
    /// </summary>
    /// <remarks>
    /// An entry can reference an entry that is reconciled after it: a queue references a channel endpoint that a
    /// later step imports, and a queue overflows into another queue in its own step. The reference it was imported
    /// with is correct only until the referenced entry turns out to already exist on the destination under a
    /// different identifier, so every catalog is asked to repair its entries after each step rather than trusting
    /// that the plan is ordered perfectly.
    /// </remarks>
    /// <param name="context">The recipe execution context that identifies the running import and collects errors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RepairReferencesAsync(RecipeExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the identity of an exported entry - the value that decides whether the destination already holds it.
    /// </summary>
    /// <param name="entry">The exported entry.</param>
    /// <returns>The identity, or <see langword="null"/> when the entry carries nothing that identifies it.</returns>
    string GetIdentity(JsonObject entry);
}
