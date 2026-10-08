using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that captures the settings for the built-in live website search documentation tool
/// instance source. The instance queries a site's own search API on every request (defaulting to the
/// WordPress REST search endpoint) instead of crawling it, and maps the JSON response to results.
/// </summary>
internal sealed class WebsiteSearchToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteSearchToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebsiteSearchToolInstanceDisplayDriver(IStringLocalizer<WebsiteSearchToolInstanceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        return Initialize<WebsiteSearchToolInstanceViewModel>("WebsiteSearchToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<WebsiteSearchToolSettings>();

            model.BaseUrl = settings.BaseUrl;
            model.SearchPath = settings.SearchPath;
            model.QueryParameter = settings.QueryParameter;
            model.ExtraQuery = settings.ExtraQuery;
            model.ResultsPath = settings.ResultsPath;
            model.TitlePath = settings.TitlePath;
            model.UrlPath = settings.UrlPath;
            model.SnippetPath = settings.SnippetPath;
            model.MaxResults = settings.MaxResults;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        var model = new WebsiteSearchToolInstanceViewModel();

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

        if (string.IsNullOrWhiteSpace(model.SearchPath))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SearchPath), S["The search endpoint path is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.QueryParameter))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.QueryParameter), S["The query parameter name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.TitlePath))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TitlePath), S["The title field path is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.UrlPath))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UrlPath), S["The URL field path is required."]);
        }

        if (model.MaxResults.HasValue && model.MaxResults.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxResults), S["The maximum results must be greater than zero."]);
        }

        instance.Put(new WebsiteSearchToolSettings
        {
            BaseUrl = model.BaseUrl?.Trim(),
            SearchPath = model.SearchPath?.Trim(),
            QueryParameter = model.QueryParameter?.Trim(),
            ExtraQuery = string.IsNullOrWhiteSpace(model.ExtraQuery) ? null : model.ExtraQuery.Trim(),
            ResultsPath = string.IsNullOrWhiteSpace(model.ResultsPath) ? null : model.ResultsPath.Trim(),
            TitlePath = model.TitlePath?.Trim(),
            UrlPath = model.UrlPath?.Trim(),
            SnippetPath = string.IsNullOrWhiteSpace(model.SnippetPath) ? null : model.SnippetPath.Trim(),
            MaxResults = model.MaxResults,
        });

        return Edit(instance, context);
    }

    private static bool IsSource(AIToolInstance instance)
        => string.Equals(instance.Source, DocumentationToolConstants.WebsiteSearchSourceName, StringComparison.OrdinalIgnoreCase);
}
