using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that captures the settings for the built-in sitemap crawling documentation search tool
/// instance source, such as the base URL of the documentation site and the crawl limits.
/// </summary>
internal sealed class SitemapDocumentationToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapDocumentationToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="endpointPolicy">The policy that keeps the instance's addresses off the site's own network.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SitemapDocumentationToolInstanceDisplayDriver(IAIEndpointPolicy endpointPolicy, IStringLocalizer<SitemapDocumentationToolInstanceDisplayDriver> stringLocalizer)
    {
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        return Initialize<SitemapDocumentationToolInstanceViewModel>("SitemapDocumentationToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<SitemapDocumentationToolSettings>();

            model.BaseUrl = settings.BaseUrl;
            model.SitemapUrl = settings.SitemapUrl;
            model.MaxResults = settings.MaxResults;
            model.MaxPages = settings.MaxPages;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        var model = new SitemapDocumentationToolInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The base URL is required."]);
        }
        else if (!Uri.TryCreate(model.BaseUrl.Trim(), UriKind.Absolute, out var baseUrl) ||
            (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["The base URL must be an absolute HTTP or HTTPS URL."]);
        }
        else if (!string.IsNullOrWhiteSpace(model.BaseUrl) && !_endpointPolicy.IsAllowed(model.BaseUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }

        if (!string.IsNullOrWhiteSpace(model.SitemapUrl) &&
            (!Uri.TryCreate(model.SitemapUrl.Trim(), UriKind.Absolute, out var sitemapUrl) ||
            (sitemapUrl.Scheme != Uri.UriSchemeHttp && sitemapUrl.Scheme != Uri.UriSchemeHttps)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SitemapUrl), S["The sitemap URL must be an absolute HTTP or HTTPS URL."]);
        }
        else if (!string.IsNullOrWhiteSpace(model.SitemapUrl) && !_endpointPolicy.IsAllowed(model.SitemapUrl))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SitemapUrl), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }

        if (model.MaxResults.HasValue && model.MaxResults.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxResults), S["The maximum results must be greater than zero."]);
        }

        if (model.MaxPages.HasValue && model.MaxPages.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxPages), S["The maximum pages must be greater than zero."]);
        }

        instance.Put(new SitemapDocumentationToolSettings
        {
            BaseUrl = model.BaseUrl?.Trim(),
            SitemapUrl = string.IsNullOrWhiteSpace(model.SitemapUrl) ? null : model.SitemapUrl.Trim(),
            MaxResults = model.MaxResults,
            MaxPages = model.MaxPages,
        });

        return Edit(instance, context);
    }

    private static bool IsSource(AIToolInstance instance)
        => string.Equals(instance.Source, DocumentationToolConstants.SitemapSourceName, StringComparison.OrdinalIgnoreCase);
}
