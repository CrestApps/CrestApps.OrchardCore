using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Core.Recipes;

/// <summary>
/// Exports ingestion sources, such as file sources and web crawlers, as one recipe step that holds the sources in one
/// array, ordered by display text. Every secret in a source's extension data is left out, and so are the members the
/// storing site owns.
/// </summary>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TSource">The ingestion source type.</typeparam>
public abstract class IngestionSourceDeploymentSource<TStep, TSource> : CatalogDeploymentSourceBase<TStep, TSource>
    where TStep : DeploymentStep
    where TSource : IngestionSource
{
    private readonly IReadCatalogManager<TSource> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestionSourceDeploymentSource{TStep, TSource}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the array the sources are written to.</param>
    /// <param name="manager">The manager that reads the sources.</param>
    protected IngestionSourceDeploymentSource(
        string entriesName,
        IReadCatalogManager<TSource> manager)
        : base(entriesName)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<TSource>> GetEntriesAsync()
        => (await _manager.GetAllAsync()).OrderBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    protected override JsonObject Export(TSource entry)
    {
        var node = CatalogDeploymentSerializer.Export(entry);

        IngestionSourceSecrets.Remove(node[nameof(IngestionSource.Properties)]);

        return node;
    }
}
