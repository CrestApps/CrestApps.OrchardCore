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
/// Display driver that captures the settings for the built-in Algolia DocSearch documentation search tool
/// instance source, such as the application identifier, the search-only API key, and the index name.
/// </summary>
internal sealed class AlgoliaDocumentationToolInstanceDisplayDriver : DisplayDriver<AIToolInstance>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgoliaDocumentationToolInstanceDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AlgoliaDocumentationToolInstanceDisplayDriver(IStringLocalizer<AlgoliaDocumentationToolInstanceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        return Initialize<AlgoliaDocumentationToolInstanceViewModel>("AlgoliaDocumentationToolInstance_Edit", model =>
        {
            var settings = instance.GetOrCreate<AlgoliaDocumentationToolSettings>();

            model.ApplicationId = settings.ApplicationId;
            model.ApiKey = settings.ApiKey;
            model.IndexName = settings.IndexName;
            model.MaxResults = settings.MaxResults;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        if (!IsSource(instance))
        {
            return null;
        }

        var model = new AlgoliaDocumentationToolInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ApplicationId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApplicationId), S["The application id is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["The search-only API key is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["The index name is required."]);
        }

        if (model.MaxResults.HasValue && model.MaxResults.Value <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxResults), S["The maximum results must be greater than zero."]);
        }

        instance.Put(new AlgoliaDocumentationToolSettings
        {
            ApplicationId = model.ApplicationId?.Trim(),
            ApiKey = model.ApiKey?.Trim(),
            IndexName = model.IndexName?.Trim(),
            MaxResults = model.MaxResults,
        });

        return Edit(instance, context);
    }

    private static bool IsSource(AIToolInstance instance)
        => string.Equals(instance.Source, DocumentationToolConstants.AlgoliaSourceName, StringComparison.OrdinalIgnoreCase);
}
