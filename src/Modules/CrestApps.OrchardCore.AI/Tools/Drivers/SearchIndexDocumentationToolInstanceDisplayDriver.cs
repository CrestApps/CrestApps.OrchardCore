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
/// Display driver that captures the settings for the built-in prebuilt search index documentation search
/// tool instance source, such as the base URL and the optional explicit index URL.
/// </summary>
internal sealed class SearchIndexDocumentationToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchIndexDocumentationToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SearchIndexDocumentationToolInstanceDisplayDriver(IStringLocalizer<SearchIndexDocumentationToolInstanceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        return Initialize<SearchIndexDocumentationToolInstanceViewModel>("SearchIndexDocumentationToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<SearchIndexDocumentationToolSettings>();

            model.BaseUrl = settings.BaseUrl;
            model.IndexUrl = settings.IndexUrl;
            model.MaxResults = settings.MaxResults;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        var model = new SearchIndexDocumentationToolInstanceViewModel();

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

        if (!string.IsNullOrWhiteSpace(model.IndexUrl) &&
            (!Uri.TryCreate(model.IndexUrl.Trim(), UriKind.Absolute, out var indexUrl) ||
            (indexUrl.Scheme != Uri.UriSchemeHttp && indexUrl.Scheme != Uri.UriSchemeHttps)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexUrl), S["The index URL must be an absolute HTTP or HTTPS URL."]);
        }

        if (model.MaxResults.HasValue && model.MaxResults.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxResults), S["The maximum results must be greater than zero."]);
        }

        instance.Put(new SearchIndexDocumentationToolSettings
        {
            BaseUrl = model.BaseUrl?.Trim(),
            IndexUrl = string.IsNullOrWhiteSpace(model.IndexUrl) ? null : model.IndexUrl.Trim(),
            MaxResults = model.MaxResults,
        });

        return Edit(instance, context);
    }

    private static bool IsSource(AIToolInstance instance)
        => string.Equals(instance.Source, DocumentationToolConstants.SearchIndexSourceName, StringComparison.OrdinalIgnoreCase);
}
