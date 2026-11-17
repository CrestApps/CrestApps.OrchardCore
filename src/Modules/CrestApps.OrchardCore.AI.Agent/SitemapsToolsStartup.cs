using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Sitemaps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete sitemaps and clear the sitemap cache, when the Sitemaps feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Sitemaps)]
public sealed class SitemapsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SitemapsToolsStartup(IStringLocalizer<SitemapsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSitemapsTool>(ListSitemapsTool.TheName)
            .WithTitle(S["List Sitemaps"])
            .WithDescription(S["Lists the sitemaps and sitemap indexes with their path, enabled state and sources (content types with frequency and priority, custom paths), and the cached sitemap files. Use it to see what search engines are given."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();

        services.AddCoreAITool<SaveSitemapTool>(SaveSitemapTool.TheName)
            .WithTitle(S["Save Sitemap"])
            .WithDescription(S["Creates a sitemap, or updates a sitemap or a sitemap index: name, path, enabled, and the sources of a sitemap (content type sources and custom path sources). The given source list replaces the existing one; the path must be unique and end in .xml."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();

        services.AddCoreAITool<DeleteSitemapTool>(DeleteSitemapTool.TheName)
            .WithTitle(S["Delete Sitemap"])
            .WithDescription(S["Deletes a sitemap or a sitemap index. Indexes that contain the sitemap are not updated."])
            .WithCategory(S["Search Engine Optimization"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<PurgeSitemapCacheTool>(PurgeSitemapCacheTool.TheName)
            .WithTitle(S["Purge Sitemap Cache"])
            .WithDescription(S["Clears the cached sitemap files, one file or all of them, so sitemaps are rebuilt on the next request. Use it after changing sitemaps or publishing many items."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();
    }
}
