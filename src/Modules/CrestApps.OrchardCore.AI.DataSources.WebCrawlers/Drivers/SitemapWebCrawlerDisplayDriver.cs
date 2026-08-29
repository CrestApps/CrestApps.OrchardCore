using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies.Sitemap;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Drivers;

/// <summary>
/// Display driver for the sitemap crawl-strategy settings. Only renders for crawlers whose strategy is
/// <see cref="WebCrawlerConstants.Strategies.Sitemap"/>.
/// </summary>
internal sealed class SitemapWebCrawlerDisplayDriver : DisplayDriver<WebCrawler>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapWebCrawlerDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SitemapWebCrawlerDisplayDriver(IStringLocalizer<SitemapWebCrawlerDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WebCrawler crawler, BuildEditorContext context)
    {
        if (!IsSitemap(crawler))
        {
            return null;
        }

        return Initialize<SitemapWebCrawlerViewModel>("SitemapWebCrawler_Edit", model =>
        {
            if (crawler.TryGet<SitemapWebCrawlerMetadata>(out var metadata))
            {
                model.BaseUrl = metadata.BaseUrl;
                model.SitemapUrl = metadata.SitemapUrl;
                model.MaxPages = metadata.MaxPages;
                model.MaxConcurrentRequests = metadata.MaxConcurrentRequests;
                model.RequestTimeoutSeconds = metadata.RequestTimeoutSeconds;
                model.UserAgent = metadata.UserAgent;
                model.IncludeUrlPatterns = JoinPatterns(metadata.IncludeUrlPatterns);
                model.ExcludeUrlPatterns = JoinPatterns(metadata.ExcludeUrlPatterns);
            }
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(WebCrawler crawler, UpdateEditorContext context)
    {
        if (!IsSitemap(crawler))
        {
            return null;
        }

        var model = new SitemapWebCrawlerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.BaseUrl) && string.IsNullOrWhiteSpace(model.SitemapUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["Provide a base URL or an explicit sitemap URL."]);
        }

        if (!string.IsNullOrWhiteSpace(model.BaseUrl) && !Uri.TryCreate(model.BaseUrl.Trim(), UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The base URL must be an absolute URL, for example https://example.com."]);
        }

        if (!string.IsNullOrWhiteSpace(model.SitemapUrl) && !Uri.TryCreate(model.SitemapUrl.Trim(), UriKind.Absolute, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SitemapUrl), S["The sitemap URL must be an absolute URL, for example https://example.com/sitemap.xml."]);
        }

        ValidatePatterns(context, nameof(model.IncludeUrlPatterns), model.IncludeUrlPatterns);
        ValidatePatterns(context, nameof(model.ExcludeUrlPatterns), model.ExcludeUrlPatterns);

        crawler.Put(new SitemapWebCrawlerMetadata
        {
            BaseUrl = model.BaseUrl?.Trim(),
            SitemapUrl = model.SitemapUrl?.Trim(),
            MaxPages = model.MaxPages,
            MaxConcurrentRequests = model.MaxConcurrentRequests,
            RequestTimeoutSeconds = model.RequestTimeoutSeconds,
            UserAgent = string.IsNullOrWhiteSpace(model.UserAgent) ? null : model.UserAgent.Trim(),
            IncludeUrlPatterns = SplitPatterns(model.IncludeUrlPatterns),
            ExcludeUrlPatterns = SplitPatterns(model.ExcludeUrlPatterns),
        });

        return Edit(crawler, context);
    }

    private void ValidatePatterns(UpdateEditorContext context, string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var pattern in SplitPatterns(value))
        {
            try
            {
                _ = Regex.Match(string.Empty, pattern);
            }
            catch (ArgumentException)
            {
                context.Updater.ModelState.AddModelError(Prefix, field, S["'{0}' is not a valid regular expression.", pattern]);
            }
        }
    }

    private static bool IsSitemap(WebCrawler crawler)
        => string.Equals(crawler.Source, WebCrawlerConstants.Strategies.Sitemap, StringComparison.OrdinalIgnoreCase);

    private static string JoinPatterns(IEnumerable<string> patterns)
        => patterns is null ? null : string.Join('\n', patterns);

    private static List<string> SplitPatterns(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
