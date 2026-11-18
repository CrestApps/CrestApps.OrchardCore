using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.WebCrawlers.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.WebCrawlers;

/// <summary>
/// Registers the AI tools that list, save, delete and run web crawlers. Saving, deleting and running a crawler crawls a
/// website and spends embedding tokens, so those tools ask for approval.
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
        services.AddCoreAITool<ListAIWebCrawlersTool>(ListAIWebCrawlersTool.TheName)
            .WithTitle(S["List AI Web Crawlers"])
            .WithDescription(S["Lists the web crawlers that feed Web AI data sources, with site, sitemap, limits, URL patterns, data source, enabled state and schedule, with optional name search or one crawler by id."])
            .WithCategory(S["AI Knowledge"])
            .Selectable();

        services.AddCoreAITool<SaveAIWebCrawlerTool>(SaveAIWebCrawlerTool.TheName)
            .WithTitle(S["Save AI Web Crawler"])
            .WithDescription(S["Creates a web crawler for a public website, or updates one by id. It needs an existing Web AI data source. Saving queues a full synchronization of that data source, which crawls the site and spends embedding tokens in the background."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIWebCrawlerTool>(DeleteAIWebCrawlerTool.TheName)
            .WithTitle(S["Delete AI Web Crawler"])
            .WithDescription(S["Deletes a web crawler. Its pages stop being refreshed in the knowledge base."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RunAIWebCrawlerTool>(RunAIWebCrawlerTool.TheName)
            .WithTitle(S["Run AI Web Crawler"])
            .WithDescription(S["Re-crawls one website now and queues its new, changed and removed pages for indexing, returning the three counts. It sends requests to the website and spends embedding tokens for every changed page."])
            .WithCategory(S["AI Knowledge"])
            .RequireApproval()
            .Selectable();
    }
}
