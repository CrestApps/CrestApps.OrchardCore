using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources;

/// <summary>
/// Registers the AI tools that list, synchronize and test-search AI data sources. The tools that save and delete a data
/// source live in the AI Agent module, because they must not change the data source of the assistant running the turn
/// and that check belongs to the AI Agent module.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIDataSourcesTool>(ListAIDataSourcesTool.TheName)
            .WithTitle(S["List AI Data Sources"])
            .WithDescription(S["Lists the AI data sources (knowledge bases) with their source type, destination index and field mapping, with optional name search, source type filter, paging, or one data source by id. Also returns the source types that can be created on this site. Secrets are never returned."])
            .WithCategory(S["AI Knowledge"])
            .Selectable();

        services.AddCoreAITool<SyncAIDataSourceTool>(SyncAIDataSourceTool.TheName)
            .WithTitle(S["Sync AI Data Source"])
            .WithDescription(S["Queues a full synchronization of one data source into its AI knowledge base index, which re-reads the source and generates embeddings in the background. Use it when answers are stale; it costs embedding tokens in proportion to the size of the source."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SearchAIDataSourceTool>(SearchAIDataSourceTool.TheName)
            .WithTitle(S["Search AI Data Source"])
            .WithDescription(S["Runs a test vector search of one data source for a query and returns the best matching chunks with their titles and references, as an assistant would receive them. Use it to check what a knowledge base returns before or after a change; it makes one embedding call."])
            .WithCategory(S["AI Knowledge"])
            .Selectable();
    }
}
