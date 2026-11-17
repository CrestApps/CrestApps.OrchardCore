using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that searches an index the way the search page of the site does, when the Search feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Search)]
public sealed class SearchToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SearchToolsStartup(IStringLocalizer<SearchToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SearchIndexTool>(SearchIndexTool.TheName)
            .WithTitle(S["Search Index"])
            .WithDescription(S["Runs a plain-text search against an index profile (the default site search index when none is named) the way the site's search page does, and returns the matching content items with highlights, paged. Use it to check what visitors find for a term or to verify an index after indexing."])
            .WithCategory(S["Search Management"])
            .Selectable();
    }
}
