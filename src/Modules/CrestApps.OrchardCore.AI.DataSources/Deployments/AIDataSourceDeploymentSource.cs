using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

/// <summary>
/// Writes the selected AI data sources to a deployment plan, in the shape <c>AIDataSourceStep</c> reads.
/// </summary>
internal sealed class AIDataSourceDeploymentSource : CatalogDeploymentSourceBase<AIDataSourceDeploymentStep, AIDataSource>
{
    private readonly IAIDataSourceStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    public AIDataSourceDeploymentSource(IAIDataSourceStore store)
        : base("DataSources")
    {
        _store = store;
    }

    protected override async Task<IEnumerable<AIDataSource>> GetEntriesAsync()
        => await _store.GetAllAsync();

    protected override bool IsSelected(AIDataSourceDeploymentStep step, AIDataSource entry)
        => IsSelectedByKey(step.IncludeAll, step.SourceIds, entry.ItemId);

    // The source travels too: without it the recipe step reads every data source as a search index profile.
    protected override JsonObject Export(AIDataSource entry)
        => new()
        {
            { "ItemId", entry.ItemId },
            { "Source", entry.Source },
            { "DisplayText", entry.DisplayText },
            { "SourceIndexProfileName", entry.SourceIndexProfileName },
            { "AIKnowledgeBaseIndexProfileName", entry.AIKnowledgeBaseIndexProfileName },
            { "KeyFieldName", entry.KeyFieldName },
            { "TitleFieldName", entry.TitleFieldName },
            { "ContentFieldName", entry.ContentFieldName },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
            { "Author", entry.Author },
            { "Properties", entry.Properties is not null ? JsonSerializer.SerializeToNode(entry.Properties) : null },
        };
}
