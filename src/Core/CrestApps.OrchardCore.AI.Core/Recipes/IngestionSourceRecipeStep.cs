using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Imports ingestion sources, such as file sources and web crawlers, that a recipe step carries in one array. A source is
/// matched by its <c>ItemId</c>, then by its display text within the same source kind, so a second run updates it.
/// </summary>
/// <remarks>
/// A source's kind must be one that is registered on the site, which the derived step checks in
/// <see cref="IsKnownSource"/>. Secrets are never carried by an export: when an update leaves one out, the stored secret
/// is kept, and a new source without one waits for an administrator to enter it.
/// </remarks>
/// <typeparam name="TSource">The ingestion source type.</typeparam>
public abstract class IngestionSourceRecipeStep<TSource> : CatalogRecipeStepBase<TSource>
    where TSource : IngestionSource
{
    private readonly ISourceCatalogManager<TSource> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionSourceRecipeStep{TSource}"/> class.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="entriesName">The name of the step member that holds the array of sources.</param>
    /// <param name="manager">The manager the sources are created and updated through.</param>
    protected IngestionSourceRecipeStep(
        string stepName,
        string entriesName,
        ISourceCatalogManager<TSource> manager)
        : base(stepName, entriesName)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
    }

    /// <summary>
    /// Says whether a source kind is registered on this site.
    /// </summary>
    /// <param name="source">The source kind the recipe names.</param>
    /// <returns><see langword="true"/> when the kind is registered.</returns>
    protected abstract bool IsKnownSource(string source);

    /// <summary>
    /// Gets the error reported for a source that names no kind, or a kind that is not registered.
    /// </summary>
    /// <param name="displayText">The display text of the source.</param>
    /// <param name="source">The source kind the recipe names, if any.</param>
    /// <returns>The error message.</returns>
    protected abstract string UnknownSourceError(string displayText, string source);

    /// <inheritdoc/>
    protected override ValueTask<TSource> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    /// <inheritdoc/>
    protected override async ValueTask<CatalogRecipeLookup<TSource>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = Text(token, nameof(IngestionSource.Source));
        var displayText = Text(token, nameof(IngestionSource.DisplayText))?.Trim();

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(displayText))
        {
            return CatalogRecipeLookup<TSource>.NotFound;
        }

        var entry = (await _manager.GetAsync(source))
            .FirstOrDefault(item => string.Equals(item.DisplayText?.Trim(), displayText, StringComparison.OrdinalIgnoreCase));

        return entry is null ? CatalogRecipeLookup<TSource>.NotFound : CatalogRecipeLookup<TSource>.Found(entry);
    }

    /// <inheritdoc/>
    protected override async ValueTask<TSource> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = Text(token, nameof(IngestionSource.Source));

        if (string.IsNullOrEmpty(source) || !IsKnownSource(source))
        {
            context.Errors.Add(UnknownSourceError(Text(token, nameof(IngestionSource.DisplayText)), source));

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    /// <inheritdoc/>
    protected override async ValueTask UpdateAsync(TSource entry, JsonObject token)
    {
        if (token[nameof(IngestionSource.Properties)] is JsonObject properties)
        {
            var stored = JsonSerializer.SerializeToNode(entry.Properties, CatalogDeploymentSerializer.Options) as JsonObject;

            IngestionSourceSecrets.KeepStored(properties, stored);
        }

        await _manager.UpdateAsync(entry, token);
    }

    /// <inheritdoc/>
    protected override ValueTask<ValidationResultDetails> ValidateAsync(TSource entry)
        => _manager.ValidateAsync(entry);

    /// <inheritdoc/>
    protected override ValueTask CreateAsync(TSource entry)
        => _manager.CreateAsync(entry);

    private static string Text(JsonObject token, string name)
        => token[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
