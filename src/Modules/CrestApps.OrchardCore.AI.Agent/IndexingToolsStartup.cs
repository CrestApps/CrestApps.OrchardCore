using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Indexing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, read, save, synchronize, rebuild and delete search index profiles, when the Indexing
/// feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Indexing)]
public sealed class IndexingToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexingToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IndexingToolsStartup(IStringLocalizer<IndexingToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListIndexProfilesTool>(ListIndexProfilesTool.TheName)
            .WithTitle(S["List Index Profiles"])
            .WithDescription(S["Lists the search index profiles (name, provider, type, index name, indexed content types, created date) with an optional name or provider filter, and the provider and type combinations available to create a new one. Use it to find an index before searching, querying or rebuilding it."])
            .WithCategory(S["Search Management"])
            .Selectable();

        services.AddCoreAITool<GetIndexProfileTool>(GetIndexProfileTool.TheName)
            .WithTitle(S["Get Index Profile"])
            .WithDescription(S["Returns one index profile with all its settings (content types, culture, latest or published, analyzer, default search fields) and the names of the indexed fields. Use it before writing a search query or changing the profile."])
            .WithCategory(S["Search Management"])
            .Selectable();

        services.AddCoreAITool<SaveIndexProfileTool>(SaveIndexProfileTool.TheName)
            .WithTitle(S["Save Index Profile"])
            .WithDescription(S["Creates an index profile for a provider and type (for example Lucene and Content) or updates an existing one by id or name. Creating also creates the physical index and starts indexing in the background; the index name, provider and type cannot be changed afterwards."])
            .WithCategory(S["Search Management"])
            .Selectable();

        services.AddCoreAITool<SynchronizeIndexProfileTool>(SynchronizeIndexProfileTool.TheName)
            .WithTitle(S["Synchronize Index Profile"])
            .WithDescription(S["Triggers a background synchronization of one or more indexes, or with `reset` re-indexes every item from the beginning while keeping the existing entries. Use it when search results are stale; use `rebuildIndexProfile` only when the index structure changed."])
            .WithCategory(S["Search Management"])
            .Selectable();

        services.AddCoreAITool<RebuildIndexProfileTool>(RebuildIndexProfileTool.TheName)
            .WithTitle(S["Rebuild Index Profile"])
            .WithDescription(S["Deletes and recreates the physical index of one or more index profiles and re-indexes everything in the background. Search on those indexes returns incomplete results until indexing finishes."])
            .WithCategory(S["Search Management"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteIndexProfileTool>(DeleteIndexProfileTool.TheName)
            .WithTitle(S["Delete Index Profile"])
            .WithDescription(S["Deletes one or more index profiles together with their physical indexes. Check first that no saved query, the site search settings or a content picker uses the index."])
            .WithCategory(S["Search Management"])
            .RequireApproval()
            .Selectable();
    }
}
