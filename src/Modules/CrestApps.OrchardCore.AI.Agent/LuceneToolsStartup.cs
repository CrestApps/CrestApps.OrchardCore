using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that runs a JSON query against a search index, when the Lucene feature is on. The same tool
/// serves Elasticsearch indexes and is also registered by <see cref="ElasticsearchToolsStartup"/>.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Lucene)]
public sealed class LuceneToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LuceneToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LuceneToolsStartup(IStringLocalizer<LuceneToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<RunSearchQueryTool>(RunSearchQueryTool.TheName)
            .WithTitle(S["Run Search Query"])
            .WithDescription(S["Runs an ad-hoc JSON query (Elasticsearch-style DSL, with optional Liquid parameters) against a Lucene or Elasticsearch index profile and returns the matching documents and the count. Use it to test a query before saving it with `saveQuery`; not available for Azure AI Search indexes."])
            .WithCategory(S["Search Management"])
            .Selectable();
    }
}
