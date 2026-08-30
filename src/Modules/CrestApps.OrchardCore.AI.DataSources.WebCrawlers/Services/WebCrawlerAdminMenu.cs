using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Services;

/// <summary>
/// Adds the Web Crawlers entry to the Artificial Intelligence admin menu.
/// </summary>
public sealed class WebCrawlerAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebCrawlerAdminMenu(IStringLocalizer<WebCrawlerAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Web Crawlers"], S["Web Crawlers"].PrefixPosition(), webCrawlers => webCrawlers
                    .AddClass("ai-web-crawlers")
                    .Id("aiWebCrawlers")
                    .Action("Index", "WebCrawlers", "CrestApps.OrchardCore.AI.DataSources.WebCrawlers")
                    .Permission(WebCrawlerPermissions.ManageWebCrawlers)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
